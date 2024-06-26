using System.Text;

namespace Net;

partial class IocpClientProtocol : IDisposable
{
    public AutoResetEvent Done = new AutoResetEvent(false);

    enum Command
    {
        None = 0,
        Login = 1,
        Active = 2,
        Dir = 3,
        FileList = 4,
        Download = 5,
        Data = 6,
        Message = 7,
        Upload = 8,
        SendFile = 9
    }

    bool IsLogin { get; set; } = false;

    public int PacketSize { get; set; } = 8 * 1024;

    public string FilePath { get; private set; } = "";

    byte[]? ReadBuffer { get; set; } = null;

    FileStream? FileStream { get; set; } = null;

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

    public UserInfo UserInfo { get; } = new();

    public void Dispose()
    {
        FilePath = "";
        FileStream?.Close();
        FileStream = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 向服务端发送消息，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送失败
    /// </summary>
    /// <param name="msg">消息内容</param>
    public void SendMessage(string message)
    {
        CommandComposer.Clear();
        CommandComposer.AddRequest();
        CommandComposer.AddCommand(ProtocolKey.Message);
        var bufferMsg = Encoding.UTF8.GetBytes(message);
        try
        {
            SendCommand(bufferMsg, 0, bufferMsg.Length);
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

    private void ProcessCommand(byte[] buffer, int offset, int count)
    {
        ////CommandComposer.Clear();
        ////CommandComposer.AddResponse();
        ////CommandComposer.AddCommand(CommandParser.Command);
        var command = StrToCommand(CommandParser.Command);
        //if (!CheckLogin(command)) //检测登录
        //    return CommandFail(ProtocolCode.UserHasLogined, "");
        switch (command)
        {
            case Command.Login:
                DoLogin();
                return;
            case Command.Active:
                DoActive();
                return;
            case Command.Message:
                DoMessage(buffer, offset, count);
                return;
            case Command.Upload:
                DoUpload();
                return;
            case Command.Download:
                DoDownload();
                return;
            case Command.SendFile:
                DoSendFile();
                return;
            case Command.Data:
                DoData(buffer, offset, count);
                return;
            default:
                return;
        };
    }

    private static Command StrToCommand(string command)
    {
        if (compare(ProtocolKey.Active))
            return Command.Active;
        else if (compare(ProtocolKey.Login))
            return Command.Login;
        else if (compare(ProtocolKey.Message))
            return Command.Message;
        else if (compare(ProtocolKey.Dir))
            return Command.Dir;
        else if (compare(ProtocolKey.FileList))
            return Command.FileList;
        else if (compare(ProtocolKey.Download))
            return Command.Download;
        else if (compare(ProtocolKey.Upload))
            return Command.Upload;
        else if (compare(ProtocolKey.SendFile))
            return Command.SendFile;
        else if (compare(ProtocolKey.Data))
            return Command.Data;
        else
            return Command.None;
        bool compare(string key)
        {
            return command.Equals(key, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private void DoActive()
    {
        if (CheckErrorCode())
        {
            IsLogin = true;
        }
        else
            IsLogin = false;
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

    private void DoLogin()
    {
        if (CheckErrorCode())//返回登录成功
        {
            UserInfo.Id = CommandParser.Values[1];
            UserInfo.Name = CommandParser.Values[2];
            IsLogin = true;
        }
        else
        {
            IsLogin = false;
        }
        Done.Set();//登录结束
    }

    private void DoDownload()
    {
        if (CheckErrorCode())//文件在服务端是否在使用、是否存在
        {
            if (!File.Exists(FilePath))//本地不存在，则创建
            {
                string dir = FilePath.Substring(0, FilePath.LastIndexOf("\\"));
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                try
                {
                    FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                }
                catch { }
            }
        }
    }

    private void DoSendFile()
    {
        if (CheckErrorCode())
        {
            if (IsSendingFile)//上传文件中
            {
                if (FileStream.Position < FileStream.Length) //发送具体数据
                {
                    CommandComposer.Clear();
                    CommandComposer.AddRequest();
                    CommandComposer.AddCommand(ProtocolKey.Data);

                    if (ReadBuffer == null)
                        ReadBuffer = new byte[PacketSize];
                    int count = FileStream.Read(ReadBuffer, 0, PacketSize);
                    SendCommand(ReadBuffer, 0, count);
                }
            }
            else
            {
                CommandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize);
                FileSize = fileSize;
            }
        }
    }

    private void DoData(byte[] buffer, int offset, int count)
    {
        if (CheckErrorCode())
        {
            if (IsSendingFile)//上传文件中
            {
                if (FileStream.Position < FileStream.Length) //发送具体数据
                {
                    CommandComposer.Clear();
                    CommandComposer.AddRequest();
                    CommandComposer.AddCommand(ProtocolKey.Data);

                    if (ReadBuffer == null)
                        ReadBuffer = new byte[PacketSize];
                    int size = FileStream.Read(ReadBuffer, 0, PacketSize);//读取剩余文件数据
                    SendCommand(ReadBuffer, 0, size);
                }
                else //发送文件数据结束
                {
                    IsSendingFile = false;
                    //StaticResetevent.Done.Set();//上传结束 
                    PacketSize = PacketSize / 8;//文件传输时将包大小放大8倍,传输完成后还原为原来大小
                    HandleUpload();
                }
            }
            else//下载文件
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
                //HACK: int offset = commandLen + sizeof(int) + sizeof(int);//前8个字节为包长度+命令长度
                //HACK: size = PacketLength - offset;
                FileStream.Write(buffer, offset, count);
                ReceviedLength += count;
                if (ReceviedLength >= FileSize)
                {
                    FileStream.Close();
                    FileStream.Dispose();
                    ReceviedLength = 0;
#if DEBUG
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("文件下载成功，完成时间{0}", DateTime.Now);
                    Console.ForegroundColor = ConsoleColor.White;
#endif

                    //StaticResetevent.Done.Set();//下载完成
                    HandleDownload();
                }
            }
        }
    }

    private void DoUpload()
    {
        if (FileStream != null)
        {
            if (IsSendingFile)
            {
                CommandComposer.Clear();
                CommandComposer.AddRequest();
                CommandComposer.AddCommand(ProtocolKey.SendFile);
                //CommandComposer.AddValue(ProtocolKey.FileSize, FileStream.Length);
                SendCommand();
            }
        }
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
            Done.WaitOne();//登录阻塞，强制同步
            return IsLogin;
        }
        catch (Exception E)
        {
            //记录日志
            //Logger.Error("AsyncClientFullHandlerSocket.DoLogin" + "userID:" + userID + " password:" + password + " " + E.Message);
            return false;
        }
    }


    public bool ReConnectAndLogin()//重新定义，防止使用基类的方法
    {
        if (BasicFunc.SocketConnected(Socket) && (Active()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(Socket))
            {
                try
                {
                    Socket?.Close();
                    Connect(Host, Port);
                    ReceiveAsync();
                    return Login(UserInfo.Id, UserInfo.Password);
                }
                catch (Exception E)
                {
                    //Logger.Error("AsyncClientFullHandlerSocket.ReConnectAndLogin" + "userID:" + UserID + " password:" + Password + " " + E.Message);
                    return false;
                }
            }
            else
                return true;
        }
    }

    public void Download(string dirName, string fileName, string pathLastLevel)
    {
        bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
        if (!bConnect)
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

    public void Upload(string fileFullPath, string remoteDir, string remoteName)
    {
        bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
        if (!bConnect)
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
                PacketSize = PacketSize * 8;//文件传输时设置包大小为原来的8倍，提高传输效率，传输完成后复原
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
}
