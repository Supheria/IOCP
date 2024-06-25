using log4net;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
using System.Text;

namespace Net;

public class StateObject
{
    public Socket workSocket = null;
}
public static class StaticResetevent
{
    public static AutoResetEvent Done = new AutoResetEvent(false);
}

partial class IocpClientProtocol
{
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

    int PacketLength { get; set; } = 0;

    int PacketReceived { get; set; } = 0;

    public UserInfo LoginUser { get; } = new();

    bool BnetWorkOperate { get; set; } = false;

    public int PacketSize { get; set; } = 8 * 1024;

    public string FilePath { get; private set; } = "";

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
    public string LocalFilePath { get; set; } = "";

    // HACK: public string LocalIp { get { return ((IPEndPoint)Client.Core.LocalEndPoint).Address.ToString(); } }

    FileStream? FileStream { get; set; } = null;

    bool IsSendingFile { get; set; } = false;

    byte[]? ReadBuffer { get; set; } = null;

    public UserInfo UserInfo { get; } = new();

    protected string ErrorString { get; set; } = "";

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

    /// <summary>
    /// 循环接收消息
    /// </summary>
    public void ReceiveAsync()
    {
        StateObject state = new StateObject();
        state.workSocket = Socket;
        Socket.BeginReceive(ReceiveBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);
    }

    public void ReceiveMessageHeadCallBack(IAsyncResult ar)
    {
        try
        {
            StateObject state = (StateObject)ar.AsyncState;
            var socket = state.workSocket;
            var length = socket.EndReceive(ar);
            if (length == 0)//接收到0字节表示Socket正常断开
            {
                //Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageHeadCallBack:" + "Socket disconnect");
                return;
            }
            if (length < sizeof(int))//小于四个字节表示包头未完全接收，继续接收
            {
                Socket.BeginReceive(ReceiveBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);
                return;
            }
            PacketLength = BitConverter.ToInt32(ReceiveBuffer.Buffer, 0); //获取包长度     
            if (NetByteOrder)
                PacketLength = IPAddress.NetworkToHostOrder(PacketLength); //把网络字节顺序转为本地字节顺序
            ReceiveBuffer.SetBufferSize(sizeof(int) + PacketLength); //保证接收有足够的空间
            socket.BeginReceive(ReceiveBuffer.Buffer, sizeof(int), PacketLength - sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageDataCallback), state);//每一次异步接收数据都挂接一个新的回调方法，保证一对一
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            //Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageHeadCallBack:" + ex.Message);
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
            //Command.Login => DoLogin(),
            //Command.Active => Active(),
            //Command.Message => DoHandleMessage(buffer, offset, count),
            //Command.Dir => DoDir(),
            //Command.FileList => DoFileList(),
            //Command.Download => DoDownload(),
            //Command.Upload => DoUpload(),
            //Command.SendFile => DoSendFile(),
            //Command.Data => DoData(buffer, offset, count),
            case Command.Active:
                DoActive();
                break;
            case Command.Message:
                DoMessage(buffer, offset, count);
                break;
            case Command.Login:
                DoLogin();
                break;
            case Command.Download:
                DoDownload();
                break;
            case Command.SendFile:
                DoSendFile();
                break;
            case Command.Data:
                DoData(buffer, offset, count);
                break;
            case Command.Upload:
                DoUpload();
                break;
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

    private void HandlePacket(byte[] buffer, int offset, int count)
    {
        if (count < sizeof(int))
            return;
        var commandLength = BitConverter.ToInt32(buffer, offset); //取出命令长度
        var command = Encoding.UTF8.GetString(buffer, offset + sizeof(int), commandLength);
        if (!CommandParser.DecodeProtocolText(command)) //解析命令
            return;
        ProcessCommand(buffer, offset + sizeof(int) + commandLength, count - sizeof(int) - sizeof(int) - commandLength); //处理命令,offset + sizeof(int) + commandLen后面的为数据，数据的长度为count - sizeof(int) - sizeof(int) - commandLength，注意是包的总长度－包长度所占的字节（sizeof(int)）－ 命令长度所占的字节（sizeof(int)） - 命令的长度
    }

    private void DoActive()
    {
        if (CheckErrorCode())
        {
            BnetWorkOperate = true;
        }
        else
            BnetWorkOperate = false;
    }

    private void DoMessage(byte[] buffer, int offset, int count)
    {
        //HACK: int offset = commandLen + sizeof(int) + sizeof(int);//前8个字节为包长度+命令长度
        //HACK: size = PacketLength - offset;
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
            BnetWorkOperate = true;
        }
        else
        {
            BnetWorkOperate = false;
        }
        StaticResetevent.Done.Set();//登录结束
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
                FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
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
                    StaticResetevent.Done.Set();//上传结束 
                    PacketSize = PacketSize / 8;//文件传输时将包大小放大8倍,传输完成后还原为原来大小
                    HandleUpload();
                }
            }
            else//下载文件
            {
                if (FileStream == null)
                    FileStream = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite);
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

                    StaticResetevent.Done.Set();//下载完成
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

    private void ReceiveMessageDataCallback(IAsyncResult ar)
    {
        try
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket client = state.workSocket;
            int bytesRead = client.EndReceive(ar);
            // 接收到0字节表示Socket正常断开
            if (bytesRead <= 0)
            {
                return;
                //Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageDataCallback:" + "Socket disconnected");
            }
            PacketReceived += bytesRead;
            // 未接收完整个包数据则继续接收
            if (PacketReceived + sizeof(int) < PacketLength)
            {
                try
                {
                    int resDataLength = PacketLength - PacketReceived - sizeof(int);
                    client.BeginReceive(ReceiveBuffer.Buffer, sizeof(int) + PacketReceived, resDataLength, SocketFlags.None, new AsyncCallback(ReceiveMessageDataCallback), state);
                    return;
                }
                catch (Exception ex)
                {
                    //Logger.Error(ex.Message);
                    //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
                    throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
                }
            }
            PacketReceived = 0;
            //HACK: int size = 0;
            int commandLen = BitConverter.ToInt32(ReceiveBuffer.Buffer, sizeof(int)); //取出命令长度
            string tmpStr = Encoding.UTF8.GetString(ReceiveBuffer.Buffer, sizeof(int) + sizeof(int), commandLen);
            HandlePacket(ReceiveBuffer.Buffer, sizeof(int), PacketLength);
            //HACK: if (CommandParser.DecodeProtocolText(tmpStr)) //解析命令，命令（除Message）完成后，必须要使用StaticResetevent这个静态信号量，保证同一时刻只有一个命令在执行
            try
            {
                //判断client.Connected不准确，所以不要使用这个来判断连接是否正常
                client.BeginReceive(ReceiveBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);//继续等待执行接收任务，实现消息循环
            }
            catch (Exception ex)
            {
                //Logger.Error(ex.Message);
                //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
                throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
            }
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e.ToString());
#endif
            //Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageDataCallback:" + e.Message);
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
            StaticResetevent.Done.WaitOne();//登录阻塞，强制同步
            return BnetWorkOperate;
        }
        catch (Exception E)
        {
            //记录日志
            ErrorString = E.Message;
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
                    Disconnect();
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

    #region 文件下载
    public void DoDownload(string dirName, string fileName, string pathLastLevel)
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
            FilePath = Path.Combine(LocalFilePath + pathLastLevel, fileName);
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
            ErrorString = E.Message;
            //Logger.Error(E.Message);
        }
    }
    #endregion
    #region 文件上传
    public void DoUpload(string fileFullPath, string remoteDir, string remoteName)
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
            ErrorString = e.Message;
            //Logger.Error(e.Message);
        }
    }
    #endregion
}
