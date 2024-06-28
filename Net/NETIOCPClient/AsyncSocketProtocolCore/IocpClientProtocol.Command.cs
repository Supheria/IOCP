using LocalUtilities.TypeToolKit.Text;
using System.IO;
using System.Text;
using System.Xml.Linq;
using static Net.ServerProtocol;

namespace Net;

partial class ClientProtocol : IocpProtocol
{
    /// <summary>
    /// 本地保存文件的路径,不含文件名
    /// </summary>
    public string RootDirectoryPath { get; set; } = "";

    public AutoResetEvent LoginDone { get; } = new(false);

    public AutoResetEvent ConnectDone { get; } = new(false);

    public delegate void HandleProgress(string progress);

    public event HandleProgress? OnUploading;

    public event HandleProgress? OnDownloading;

    public void SendMessage(string message)
    {
        try
        {
            if (!CheckConnection())
                throw new ClientProtocolException(ProtocolCode.Disconnection);
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Message);
            var buffer = Encoding.UTF8.GetBytes(message);
            SendCommand(commandComposer, buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    protected override void ProcessCommand(CommandParser commandParser, byte[] buffer, int offset, int count)
    {
        if (!CheckErrorCode(commandParser))
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
            //case ProtocolKey.CheckConnection:
            //    DoCheckConnection();
            //    return;
            default:
                return;
        };
    }

    private void DoMessage(byte[] buffer, int offset, int count)
    {
        string message = Encoding.UTF8.GetString(buffer, offset, count);
        if (!string.IsNullOrWhiteSpace(message))
        {
            HandleMessage(message);
        }
    }

    private void DoLogin()
    {
        IsLogin = true;
        HandleMessage($"{UserInfo?.Name} logined");
        ConnectDone.Set();
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
            new Task(() => OnDownloading?.Invoke($"{autoFile.Position * 100f / fileLength}%")).Start();
            // simple validation
            if (autoFile.Position != position)
                throw new ClientProtocolException(ProtocolCode.NotSameVersion);
            if (autoFile.Length >= fileLength)
            {
                autoFile.Close();
                HandleDownload();
            }
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.SendFile)
                .AppendValue(ProtocolKey.Stamp, stamp)
                .AppendValue(ProtocolKey.PacketSize, packetSize);
            SendCommand(commandComposer);
        }
        catch(Exception ex)
        {
            HandleException(ex);
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
                HandleUploaded();
                return;
            }
            new Task(() => OnUploading?.Invoke($"{autoFile.Position * 100f / autoFile.Length}%")).Start();
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
            HandleException(ex);
            // TODO: log fail
        }
    }

    public void Login(string name, string password)
    {
        UserInfo = new(name, password);
        Login();
        ConnectDone.Set();
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
            HandleException(ex);
            // TODO: log fail
            //Logger.Error("AsyncClientFullHandlerSocket.DoLogin" + "userID:" + userID + " password:" + password + " " + E.Message);
        }
    }

    public void Upload(string filePath, string remoteDir, string remoteName)
    {
        try
        {
            if (!CheckConnection())
                throw new ClientProtocolException(ProtocolCode.Disconnection);
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
            HandleException(ex);
            //记录日志
            //Logger.Error(e.Message);
        }
    }

    public void Download(string dirName, string fileName, string pathLastLevel)
    {
        try
        {
            if (!CheckConnection())
                throw new ClientProtocolException(ProtocolCode.Disconnection);
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
            HandleException(ex);
            //记录日志
            //Logger.Error(E.Message);
        }
    }

    public bool CheckConnection()
    {
        Close();
        Connect();
        Login();
        ConnectDone.WaitOne(ConstTabel.TimeoutMilliseconds);
        ////var commandComposer = new CommandComposer()
        ////    .AppendCommand(ProtocolKey.CheckConnection);
        //IsLogin = false;
        ////IsSendingAsync = false;
        //for (var i = 0; i < ConstTabel.ReconnectTimesMax; i++)
        //{
        //    //SendCommand(commandComposer);
        //    Connect();
        //    ReceiveAsync();
        //    ConnectDone.WaitOne(ConstTabel.TimeoutMilliseconds);
        //    if (IsLogin)
        //        return true;
        //    OnMessage?.Invoke($"trying reconnect: {i + 1} times");
        //}
        ////IsLogin = false;
        //Close();
        //return false;
        return true;
    }

    //private void DoCheckConnection()
    //{
    //    IsLogin = true;
    //    ConnectDone.Set();
    //}
}
