using LocalUtilities.TypeToolKit.Text;
using System.Text;
using static Net.ServerProtocol;

namespace Net;

partial class ClientProtocol : IocpProtocol
{
    public int PacketSize { get; set; } = 8 * 1024;

    /// <summary>
    /// 文件的剩余长度
    /// </summary>
    long FileSize { get; set; } = 0;

    /// <summary>
    /// 本次文件已经接收的长度
    /// </summary>
    long ReceviedLength { get; set; } = 0;

    /// <summary>
    /// 本地保存文件的路径,不含文件名
    /// </summary>
    public string RootDirectoryPath { get; set; } = "";

    bool IsSendingFile { get; set; } = false;

    public AutoResetEvent LoginDone { get; } = new(false);

    public delegate void HandleProgress(string progress);

    public event HandleProgress? OnUploading;

    Dictionary<string, AutoDisposeFileStream> FileReaders { get; } = [];

    /// <summary>
    /// 向服务端发送消息，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送失败
    /// </summary>
    /// <param name="msg">消息内容</param>
    public void SendMessage(string message)
    {
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Message);
        var buffer = Encoding.UTF8.GetBytes(message);
        try
        {
            SendCommand(commandComposer, buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            //logger.Error("SendMessage error:" + e.Message);
            if (!ReConnectAndLogin()) // 检测连接是否还在，如果断开则重连并登录
            {
                //Logger.Error("AsyncClientFullHandlerSocket.SendMessage:" + "Socket disconnect");
                throw new ClientProtocolException(ProtocolCode.Disconnection, "Server is Stoped"); //抛异常是为了让前台知道网络链路的情况         
            }
            new Task(() => SendMessage(message)).Start();
        }
    }

    protected override void ProcessCommand(CommandParser commandParser, byte[] buffer, int offset, int count)
    {
        ////CommandComposer.Clear();
        ////CommandComposer.AddResponse();
        ////CommandComposer.AddCommand(CommandParser.Command);
        //var command = StrToCommand(CommandParser.Command);
        //if (!CheckLogin(command)) //检测登录
        //    return CommandFail(ProtocolCode.UserHasLogined, "");
        if (!CheckErrorCode(commandParser))
            return;
        commandParser.GetValueAsString(ProtocolKey.Command, out var command);
        switch (command)
        {
            case ProtocolKey.Login:
                DoLogin(commandParser);
                return;
            case ProtocolKey.Active:
                DoActive();
                return;
            case ProtocolKey.Message:
                DoMessage(buffer, offset, count);
                return;
            case ProtocolKey.Upload:
                DoUpload(commandParser);
                return;
            case ProtocolKey.Download:
                DoDownload();
                return;
            case ProtocolKey.Data:
                DoData(buffer, offset, count);
                return;
            default:
                return;
        };
    }

    private void DoActive()
    {
        //if (CheckErrorCode())
        {
            IsLogin = true;
        }
        //else
        //    IsLogin = false;
    }

    private void DoMessage(byte[] buffer, int offset, int count)
    {
        string message = Encoding.UTF8.GetString(buffer, offset, count);
#if DEBUG
        if (message != string.Empty)
            Console.WriteLine("Message Recevied from Server: " + message);
#endif
        //DoHandleMessage
        if (!string.IsNullOrWhiteSpace(message))
        {
            HandleReceiveMessage(message);
        }
    }

    private void DoLogin(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.UserID, out var id) ||
            !commandParser.GetValueAsString(ProtocolKey.UserName, out var name))
            IsLogin = false;
        else
        {
            UserInfo.Id = id;
            UserInfo.Name = name;
            IsLogin = true;
        }
        LoginDone.Set();//登录结束
    }

    private void DoDownload()
    {
        if (File.Exists(FilePath))
            return;
        // 本地不存在，则创建
        // TODO: modify this strange
        var dir = FilePath.Substring(0, FilePath.LastIndexOf("\\"));
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        try
        {
            FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }
        catch { }
    }

    private void DoData(byte[] buffer, int offset, int count)
    {
        // 下载文件
        if (!IsSendingFile)
        {
            try
            {
                FileStream ??= new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite);
            }
            catch
            {
                return;
            }
            FileStream.Position = FileStream.Length; //文件移到末尾                            
            FileStream.Write(buffer, offset, count);
            ReceviedLength += count;
            if (ReceviedLength >= FileSize)
            {
                FileStream.Close();
                FileStream.Dispose();
                ReceviedLength = 0;
                HandleDownload();
            }
            return;
        }
        if (FileStream is null)
            return;
        // 发送文件数据结束
        if (FileStream.Position >= FileStream.Length) 
        {
            IsSendingFile = false;
            PacketSize /= 8;//文件传输时将包大小放大8倍,传输完成后还原为原来大小
            HandleUploaded();
            return;
        }
        // 发送具体数据
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Data);
        ReadBuffer ??= new byte[PacketSize];
        // 读取剩余文件数据
        var countRemain = FileStream.Read(ReadBuffer, 0, PacketSize);
        SendCommand(commandComposer, ReadBuffer, 0, countRemain);
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
            // TODO: log fail
        }
    }

    public bool Login(string userID, string password)
    {
        try
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Login)
                .AppendValue(ProtocolKey.UserID, userID)
                .AppendValue(ProtocolKey.Password, password);
            //CommandComposer.AddValue(ProtocolKey.Password, IocpServer.BasicFunc.MD5String(password));
            UserInfo.Password = password;
            SendCommand(commandComposer);
            LoginDone.WaitOne();//登录阻塞，强制同步
            return IsLogin;
        }
        catch (Exception E)
        {
            //记录日志
            //Logger.Error("AsyncClientFullHandlerSocket.DoLogin" + "userID:" + userID + " password:" + password + " " + E.Message);
            return false;
        }
    }

    /// <summary>
    /// 检测连接是否还在，如果断开则重连并登录
    /// </summary>
    /// <returns></returns>
    public bool ReConnectAndLogin()//重新定义，防止使用基类的方法
    {
        if (BasicFunc.SocketConnected(Socket) && Active())
            return true;
        try
        {
            Socket?.Close();
            Connect(SocketInfo.RemoteEndPoint);
            ReceiveAsync();
            return Login(UserInfo.Id, UserInfo.Password);
        }
        catch (Exception E)
        {
            //Logger.Error("AsyncClientFullHandlerSocket.ReConnectAndLogin" + "userID:" + UserID + " password:" + Password + " " + E.Message);
            return false;
        }
    }

    public void Upload(string fileFullPath, string remoteDir, string remoteName)
    {
        if (!ReConnectAndLogin())
        {
            //Logger.Error("<Upload>ClientFullHandlerSocket连接断开,并且无法重连");
            return;
        }
        try
        {
            //long fileSize = 0;
            if (!File.Exists(fileFullPath))
            {
                //Logger.Error("Start Upload file error, file is not exists: " + fileFullPath);
                return;
            }
            //FileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);//文件以共享只读方式打开
            //fileSize = FileStream.Length;
            //IsSendingFile = true;
            //PacketSize *= 8;//文件传输时设置包大小为原来的8倍，提高传输效率，传输完成后复原
            var fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var stamp = DateTime.Now.ToString();
            var autoFile = new AutoDisposeFileStream(stamp, fileStream, 5);
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
        catch (Exception e)
        {
            //记录日志
            //Logger.Error(e.Message);
        }
    }

    public void Download(string dirName, string fileName, string pathLastLevel)
    {
        if (!ReConnectAndLogin())
        {
            //Logger.Error("<DoDownload>ClientFullHandlerSocket连接断开,并且无法重连");
            return;
        }
        try
        {
            long fileSize = 0;
            FilePath = Path.Combine(RootDirectoryPath + pathLastLevel, fileName);
            if (File.Exists(FilePath))//支持断点续传，如果有未下载完成的，则接着下载
            {
                if (!BasicFunc.IsFileInUse(FilePath)) //检测文件是否正在使用中
                {
                    FileStream = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite);
                }
                else
                {
                    //Logger.Error("Start download file error, file is in use: " + fileName);
                    return;
                }
                fileSize = FileStream.Length;
            }
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Download)
                .AppendValue(ProtocolKey.DirName, dirName)
                .AppendValue(ProtocolKey.FileName, fileName)
                .AppendValue(ProtocolKey.FileLength, fileSize)
                .AppendValue(ProtocolKey.PacketSize, PacketSize);
            SendCommand(commandComposer);
        }
        catch (Exception E)
        {
            //记录日志
            //Logger.Error(E.Message);
        }
    }
}
