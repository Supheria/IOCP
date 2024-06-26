using log4net;
using log4net.Repository.Hierarchy;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Net;

partial class IocpClientProtocol
{
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
    public Socket? Socket { get; set; } = null;

    //public int TimeoutMilliseconds
    //{
    //    get => Socket.ReceiveTimeout;
    //    set
    //    {
    //        Socket.ReceiveTimeout = value;
    //        Socket.SendTimeout = value;
    //    }
    //}

    object Locker { get; } = new();

    object CloseLocker { get; } = new();

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
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (!Socket.ConnectAsync(connectArgs))
                ProcessConnect(connectArgs);
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

    private void ProcessConnect(SocketAsyncEventArgs connectArgs)
    {
        if (connectArgs.ConnectSocket is null)
        {
            Socket?.Close();
            Socket?.Dispose();
            return;
        }
        new Task(() => OnConnect?.Invoke(this)).Start();
        SocketInfo.Connect(connectArgs.ConnectSocket);
        IsConnect = true;
    }

    ManualResetEvent CloseDone { get; } = new(true);

    public void Close()
    {
        //CloseDone.WaitOne();
        //CloseDone.Reset();
        lock (CloseDone)
        {
            if (Socket is null || !IsConnect)
            {
                //CloseDone.Set();
                return;
            }
            try
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
            }
            ReceiveBuffer.Clear();
            SendBuffer.ClearPacket();
            // TODO: Dispose
            Socket.Close();
            Socket = null;
            SocketInfo.Disconnect();
            IsConnect = false;
            //CloseDone.Set();
        }

    }
    /// <summary>
    /// 循环接收消息
    /// </summary>
    public void ReceiveAsync()
    {
        var receiveArgs = new SocketAsyncEventArgs();
        receiveArgs.SetBuffer(new byte[ReceiveBuffer.BufferSize], 0, ReceiveBuffer.BufferSize);
        receiveArgs.Completed += (_, args) => ProcessReceive(args);
        if (Socket is not null && !Socket.ReceiveAsync(receiveArgs))
        {
            lock (Locker)
                ProcessReceive(receiveArgs);
        }
    }

    public void ProcessReceive(SocketAsyncEventArgs receiveArgs)
    {
        if (Socket is null ||
            receiveArgs.Buffer is null ||
            receiveArgs.BytesTransferred <= 0 ||
            receiveArgs.SocketError is not SocketError.Success)
            goto CLOSE;
        SocketInfo.Active();
        ReceiveBuffer.WriteBuffer(receiveArgs.Buffer!, receiveArgs.Offset, receiveArgs.BytesTransferred);
        // 小于四个字节表示包头未完全接收，继续接收
        while (ReceiveBuffer.DataCount > sizeof(int))
        {
            var packetLength = BitConverter.ToInt32(ReceiveBuffer.Buffer, 0);
            if (UseNetByteOrder)
                packetLength = IPAddress.NetworkToHostOrder(packetLength);
            if (packetLength > ConstTabel.ReceiveBufferMax || ReceiveBuffer.DataCount > ConstTabel.ReceiveBufferMax)
                goto CLOSE;
            if (ReceiveBuffer.DataCount < packetLength)
                goto RECEIVE;
            HandlePacket(ReceiveBuffer.Buffer, sizeof(int), packetLength);
            ReceiveBuffer.Clear(packetLength);
        }
    RECEIVE:
        ReceiveAsync();
        return;
    CLOSE:
        Close();
        return;
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

    public void SendAsync(byte[] buffer, int offset, int count)
    {
        if (Socket is null)
            return;
        var sendArgs = new SocketAsyncEventArgs();
        sendArgs.SetBuffer(buffer, offset, count);
        sendArgs.Completed += (_, args) => ProcessSend(args);
        if (!Socket.SendAsync(sendArgs))
            new Task(() => ProcessSend(sendArgs)).Start();
    }

    public void ProcessSend(SocketAsyncEventArgs sendArgs)
    {
        SocketInfo.Active();
        // 调用子类回调函数
        if (sendArgs.SocketError is not SocketError.Success)
        {
            Close();
            return;
        }
        SocketInfo.Active();
        IsSendingAsync = false;
        SendBuffer.ClearFirstPacket(); // 清除已发送的包
        if (SendBuffer.GetFirstPacket(out var offset, out var count))
        {
            IsSendingAsync = true;
            SendAsync(SendBuffer.DynamicBufferManager.Buffer, offset, count);
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
