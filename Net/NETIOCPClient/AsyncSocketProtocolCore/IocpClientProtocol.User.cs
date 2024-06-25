using log4net;
using log4net.Repository.Hierarchy;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Net;

partial class IocpClientProtocol
{
    SocketAsyncEventArgs ReceiveAsyncArgs { get; } = new();

    SocketAsyncEventArgs SendAsyncArgs { get; } = new();

    public SocketInfo SocketInfo { get; } = new();

    bool IsConnect { get; set; } = false;

    public delegate void HandleEvent(IocpClientProtocol protocol);

    public event HandleEvent? OnConnect;

    /// <summary>
    /// 标识是否有发送异步事件
    /// </summary>
    bool IsSendingAsync { get; set; } = false;

    /// <summary>
    /// Create a TCP/IP socket.
    /// </summary>
    public Socket Socket { get; set; } = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public int TimeoutMilliseconds
    {
        get => Socket.ReceiveTimeout;
        set
        {
            Socket.ReceiveTimeout = value;
            Socket.SendTimeout = value;
        }
    }

    object Locker { get; } = new();

    public IocpClientProtocol()
    {
        ReceiveAsyncArgs.Completed += (_, _) => ProcessReceive();
        SendAsyncArgs.Completed += (_, _) => ProcessSend();
    }

    public void Connect(string host, int port)
    {
        if (IsConnect)
            return;
        try
        {
            //ConnectDone.Reset();
            var connectArgs = new SocketAsyncEventArgs()
            {
                RemoteEndPoint = getIpAddress()
            };
            connectArgs.Completed += (_, args) => ProcessConnect(args);
            if (!Socket.ConnectAsync(connectArgs))
                ProcessConnect(connectArgs);
            //Core.BeginConnect(getIpAddress(), new AsyncCallback(ConnectCallback), Core);
            //ConnectDone.WaitOne();
            Host = host;
            Port = port;
        }
        catch (Exception e)
        {
            //Console.WriteLine(e.ToString());
        }
        IPEndPoint getIpAddress()
        {
            IPAddress ipAddress;
            if (Regex.Matches(host, "[a-zA-Z]").Count > 0)//支持域名解析
            {
                var ipHostInfo = Dns.GetHostEntry(host);
                ipAddress = ipHostInfo.AddressList[0];
            }
            else
            {
                ipAddress = IPAddress.Parse(host);
            }
            return new(ipAddress, port);
        }
    }

    public void Disconnect()
    {
        if (!IsConnect)
            return;
        try
        {
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
        }
        Socket.Close();
    }

    private void ProcessConnect(SocketAsyncEventArgs connectArgs)
    {
        if (connectArgs.ConnectSocket is null)
            return;
        ReceiveAsyncArgs.AcceptSocket = connectArgs.ConnectSocket;
        SendAsyncArgs.AcceptSocket = connectArgs.ConnectSocket;
        IsConnect = true;
        SocketInfo.Connect(connectArgs.ConnectSocket);
        new Task(() => OnConnect?.Invoke(this)).Start();
    }
    /// <summary>
    /// 循环接收消息
    /// </summary>
    public void ReceiveAsync()
    {
        StateObject state = new StateObject();
        state.workSocket = Socket;
        Socket.BeginReceive(ReceiveBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);
        //if (Socket is not null && !Socket.ReceiveAsync(ReceiveAsyncArgs))
        //{
        //    lock (Locker)
        //        ProcessReceive();
        //}
    }

    public void ProcessReceive()
    {

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

    public void SendAsync(int offset, int count)
    {
        SendAsyncArgs.SetBuffer(SendBuffer.DynamicBufferManager.Buffer, offset, count);
        if (!Socket.SendAsync(SendAsyncArgs))
            new Task(() => ProcessSend()).Start();
    }

    public void ProcessSend()
    {
        SocketInfo.Active();
        // 调用子类回调函数
        if (SendAsyncArgs.SocketError is SocketError.Success)
            SendComplete();
        else
            Disconnect();
    }

    public void SendComplete()
    {
        SocketInfo.Active();
        IsSendingAsync = false;
        SendBuffer.ClearFirstPacket(); // 清除已发送的包
        if (SendBuffer.GetFirstPacket(out var offset, out var count))
        {
            IsSendingAsync = true;
            SendAsync(offset, count);
        }
        //else
        //    SendCallback();
    }

    public bool CheckErrorCode()
    {
        CommandParser.GetValueAsInt(ProtocolKey.Code, out var errorCode);
        if (errorCode == ProtocolCode.Success)
            return true;
        else
        {
            //ErrorString = ProtocolCode.GetErrorCodeString(errorCode);
            return false;
        }
    }

    public bool Active()
    {
        try
        {
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Active);
            SendCommand();
            return true;
        }
        catch (Exception E)
        {
            //记录日志
            //ErrorString = E.Message;
            //Logger.Error(E.Message);
            return false;
        }
    }

    public delegate void HandleMessage(string message);

    public event HandleMessage? OnReceiveMessage;

    public void HandleReceiveMessage(string message)
    {
        new Task(() => OnReceiveMessage?.Invoke(message)).Start();
    }

    public delegate void HandleProcess();

    public event HandleProcess? OnDownload;

    public void HandleDownload()
    {
        new Task(() => OnDownload?.Invoke()).Start();
    }

    public event HandleProcess? OnUpload;

    public void HandleUpload()
    {
        new Task(() => OnUpload?.Invoke()).Start();
    }
}
