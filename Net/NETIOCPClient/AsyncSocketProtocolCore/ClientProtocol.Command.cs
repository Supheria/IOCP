using System.Text;

namespace Net;

partial class ClientProtocol : IocpProtocol
{
    /// <summary>
    /// 本地保存文件的路径,不含文件名
    /// </summary>
    public string RootDirectoryPath { get; set; } = "";

    public IocpEventHandler? OnConnect;

    public override void SendMessage(string message)
    {
        try
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Message);
            var buffer = Encoding.UTF8.GetBytes(message);
            SendCommand(commandComposer, buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            OnException?.InvokeAsync(this, ex);
        }
    }

    protected override void ProcessCommand(CommandParser commandParser, byte[] buffer, int offset, int count)
    {
        commandParser.GetValueAsInt(ProtocolKey.Code, out var errorCode);
        if ((ProtocolCode)errorCode is not ProtocolCode.Success)
            // TODO: log fail
            return;
        commandParser.GetValueAsString(ProtocolKey.Command, out var command);
        switch (command)
        {
            case ProtocolKey.Login:
                DoLogin();
                return;
            case ProtocolKey.Message:
                DoMessage(buffer, offset, count);
                return;
            case ProtocolKey.Upload:
                DoUpload(commandParser);
                return;
            case ProtocolKey.Download:
                DoDownload(commandParser, buffer, offset, count);
                return;
            default:
                return;
        };
    }

    private void DoMessage(byte[] buffer, int offset, int count)
    {
        string message = Encoding.UTF8.GetString(buffer, offset, count);
        if (!string.IsNullOrWhiteSpace(message))
        {
            OnMessage?.InvokeAsync(this, message);
        }
    }

    private void DoLogin()
    {
        IsLogin = true;
        OnMessage?.InvokeAsync(this, $"{UserInfo?.Name} logined");
    }

    private void DoDownload(CommandParser commandParser, byte[] buffer, int offset, int count)
    {
        try
        {
            if (!commandParser.GetValueAsLong(ProtocolKey.FileLength, out var fileLength) ||
                !commandParser.GetValueAsString(ProtocolKey.Stamp, out var stamp) ||
                !commandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize) ||
                !commandParser.GetValueAsLong(ProtocolKey.Position, out var position))
                throw new ServerProtocolException(ProtocolCode.ParameterError);
            if (!FileWriters.TryGetValue(stamp, out var autoFile))
                throw new ClientProtocolException(ProtocolCode.ParameterInvalid, "invalid file stamp");
            autoFile.Write(buffer, offset, count);
            OnDownloading.InvokeAsync(this, autoFile.Position * 100f / fileLength);
            // simple validation
            if (autoFile.Position != position)
                throw new ClientProtocolException(ProtocolCode.NotSameVersion);
            if (autoFile.Length >= fileLength)
            {
                autoFile.Close();
                OnDownloaded.InvokeAsync(this);
            }
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.SendFile)
                .AppendValue(ProtocolKey.Stamp, stamp)
                .AppendValue(ProtocolKey.PacketSize, packetSize);
            SendCommand(commandComposer);
        }
        catch (Exception ex)
        {
            OnException.InvokeAsync(this, ex);
            // TODO: log fail
        }
    }

    private void DoUpload(CommandParser commandParser)
    {
        try
        {
            if (!commandParser.GetValueAsString(ProtocolKey.Stamp, out var stamp) ||
                !commandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize))
                throw new ClientProtocolException(ProtocolCode.ParameterError);
            if (!FileReaders.TryGetValue(stamp, out var autoFile))
                throw new ClientProtocolException(ProtocolCode.ParameterInvalid, "invalid file stamp");
            if (autoFile.Position >= autoFile.Length)
            {
                // TODO: log success
                autoFile.Close();
                OnUploaded?.InvokeAsync(this);
                return;
            }
            OnUploading?.InvokeAsync(this, autoFile.Position * 100f / autoFile.Length);
            var buffer = new byte[packetSize];
            if (!autoFile.Read(buffer, 0, buffer.Length, out var count))
                throw new ClientProtocolException(ProtocolCode.FileIsExpired);
            //autoFile.Position += count;
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.WriteFile)
                .AppendValue(ProtocolKey.FileLength, autoFile.Length)
                .AppendValue(ProtocolKey.Stamp, stamp)
                .AppendValue(ProtocolKey.PacketSize, packetSize)
                .AppendValue(ProtocolKey.Position, autoFile.Position);
            SendCommand(commandComposer, buffer, 0, count);
        }
        catch (Exception ex)
        {
            OnException?.InvokeAsync(this, ex);
            // TODO: log fail
        }
    }

    public void Login(string name, string password)
    {
        UserInfo = new(name, password);
        Login();
    }

    private void Login()
    {
        try
        {
            if (UserInfo is null)
                throw new ClientProtocolException(ProtocolCode.NotLogined);
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Login)
                .AppendValue(ProtocolKey.UserName, UserInfo.Name)
                .AppendValue(ProtocolKey.Password, UserInfo.Password);
            SendCommand(commandComposer);
        }
        catch (Exception ex)
        {
            OnException?.InvokeAsync(this, ex);
            // TODO: log fail
            //Logger.Error("AsyncClientFullHandlerSocket.DoLogin" + "userID:" + userID + " password:" + password + " " + E.Message);
        }
    }

    public void Upload(string filePath, string remoteDir, string remoteName)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new ClientProtocolException(ProtocolCode.FileNotExist, filePath);
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var stamp = DateTime.Now.ToString();
            var autoFile = new AutoDisposeFileStream(stamp, fileStream, ConstTabel.FileStreamExpireMilliseconds);
            autoFile.OnClosed += (file) => FileReaders.Remove(file.TimeStamp);
            FileReaders[stamp] = autoFile;
            var packetSize = fileStream.Length > ConstTabel.TransferBufferMax ? ConstTabel.TransferBufferMax : fileStream.Length;
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Upload)
                .AppendValue(ProtocolKey.DirName, remoteDir)
                .AppendValue(ProtocolKey.FileName, remoteName)
                .AppendValue(ProtocolKey.Stamp, stamp)
                .AppendValue(ProtocolKey.PacketSize, packetSize);
            SendCommand(commandComposer);
        }
        catch (Exception ex)
        {
            OnException?.InvokeAsync(this, ex);
            //记录日志
            //Logger.Error(e.Message);
        }
    }

    public void Download(string dirName, string fileName, string pathLastLevel)
    {
        try
        {
            var filePath = Path.Combine(RootDirectoryPath + pathLastLevel, fileName);
            if (File.Exists(filePath))
            {
                //Logger.Error("Start Upload file error, file is not exists: " + fileFullPath);
                File.Delete(filePath);
            }
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);
            //long fileSize = 0;
            //FilePath = Path.Combine(RootDirectoryPath + pathLastLevel, fileName);
            var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            var stamp = DateTime.Now.ToString();
            var autoFile = new AutoDisposeFileStream(stamp, fileStream, ConstTabel.FileStreamExpireMilliseconds);
            autoFile.OnClosed += (file) => FileWriters.Remove(file.TimeStamp);
            FileWriters[stamp] = autoFile;
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Download)
                .AppendValue(ProtocolKey.DirName, dirName)
                .AppendValue(ProtocolKey.FileName, fileName)
                .AppendValue(ProtocolKey.Stamp, stamp);
            SendCommand(commandComposer);
        }
        catch (Exception ex)
        {
            OnException?.InvokeAsync(this, ex);
            //记录日志
            //Logger.Error(E.Message);
        }
    }
}
