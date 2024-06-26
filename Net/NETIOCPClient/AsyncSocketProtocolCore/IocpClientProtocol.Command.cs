using System.Text;

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

    /// <summary>
    /// 向服务端发送消息，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送失败
    /// </summary>
    /// <param name="msg">消息内容</param>
    public void SendMessage(string message)
    {
        CommandComposer.Clear();
        CommandComposer.AddRequest();
        CommandComposer.AddCommand(ProtocolKey.Message);
        var buffer = Encoding.UTF8.GetBytes(message);
        try
        {
            SendCommand(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            //logger.Error("SendMessage error:" + e.Message);
            if (!ReConnectAndLogin()) // 检测连接是否还在，如果断开则重连并登录
            {
                //Logger.Error("AsyncClientFullHandlerSocket.SendMessage:" + "Socket disconnect");
                throw new ClientProtocolException("Server is Stoped"); //抛异常是为了让前台知道网络链路的情况         
            }
            // TODO: here maybe dead loop
            SendMessage(message);
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
                DoUpload();
                return;
            case ProtocolKey.Download:
                DoDownload();
                return;
            case ProtocolKey.SendFile:
                DoSendFile(commandParser);
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

    private void DoSendFile(CommandParser commandParser)
    {
        if (!IsSendingFile)
        {
            commandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize);
            FileSize = fileSize;
            return;
        }
        if (FileStream is null || FileStream.Position >= FileStream.Length)
            return;
        // 上传文件中
        // 发送具体数据
        CommandComposer.Clear();
        CommandComposer.AddRequest();
        CommandComposer.AddCommand(ProtocolKey.Data);
        ReadBuffer ??= new byte[PacketSize];
        var count = FileStream.Read(ReadBuffer, 0, PacketSize);
        SendCommand(ReadBuffer, 0, count);
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
            HandleUpload();
            return;
        }
        // 发送具体数据
        CommandComposer.Clear();
        CommandComposer.AddRequest();
        CommandComposer.AddCommand(ProtocolKey.Data);
        ReadBuffer ??= new byte[PacketSize];
        // 读取剩余文件数据
        var countRemain = FileStream.Read(ReadBuffer, 0, PacketSize);
        SendCommand(ReadBuffer, 0, countRemain);
    }

    private void DoUpload()
    {
        if (FileStream is null || !IsSendingFile)
            return;
        CommandComposer.Clear();
        CommandComposer.AddRequest();
        CommandComposer.AddCommand(ProtocolKey.SendFile);
        //CommandComposer.AddValue(ProtocolKey.FileSize, FileStream.Length);
        SendCommand();
    }

    public bool Login(string userID, string password)
    {
        try
        {
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Login);
            CommandComposer.AddValue(ProtocolKey.UserID, userID);
            //CommandComposer.AddValue(ProtocolKey.Password, IocpServer.BasicFunc.MD5String(password));
            CommandComposer.AddValue(ProtocolKey.Password, password);
            UserInfo.Password = password;
            SendCommand();
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
            long fileSize = 0;
            if (File.Exists(fileFullPath))
            {
                FileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);//文件以共享只读方式打开
                fileSize = FileStream.Length;
                IsSendingFile = true;
                PacketSize *= 8;//文件传输时设置包大小为原来的8倍，提高传输效率，传输完成后复原
            }
            else
            {
                //Logger.Error("Start Upload file error, file is not exists: " + fileFullPath);
                return;
            }
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Upload);
            CommandComposer.AddValue(ProtocolKey.DirName, remoteDir);
            CommandComposer.AddValue(ProtocolKey.FileName, remoteName);
            CommandComposer.AddValue(ProtocolKey.FileSize, fileSize);
            CommandComposer.AddValue(ProtocolKey.PacketSize, PacketSize);
            SendCommand();
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
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Download);
            CommandComposer.AddValue(ProtocolKey.DirName, dirName);
            CommandComposer.AddValue(ProtocolKey.FileName, fileName);
            CommandComposer.AddValue(ProtocolKey.FileSize, fileSize);
            CommandComposer.AddValue(ProtocolKey.PacketSize, PacketSize);
            SendCommand();
        }
        catch (Exception E)
        {
            //记录日志
            //Logger.Error(E.Message);
        }
    }
}
