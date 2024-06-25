using log4net;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Net;

partial class IocpClientProtocol
{
    // HACK: public static ILog Logger;

    public static ManualResetEvent ConnectDone { get; } = new(false);

    public static ManualResetEvent SendDone { get; } = new(false);

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
    public Socket Core { get; set; } = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public int TimeoutMilliseconds
    {
        get => Core.ReceiveTimeout;
        set
        {
            Core.ReceiveTimeout = value;
            Core.SendTimeout = value;
        }
    }

    public IocpClientProtocol()
    {

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
            if (!Core.ConnectAsync(connectArgs))
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
            Core.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
        }
        Core.Close();
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

    public void SendAsync(int offset, int count)
    {
        SendAsyncArgs.SetBuffer(SendBuffer.DynamicBufferManager.Buffer, offset, count);
        if (!Core.SendAsync(SendAsyncArgs))
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

    //public IocpClientProtocol()
    //    : base()
    //{
    //    DateTime currentTime = DateTime.Now;
    //    log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
    //    log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
    //    Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    //}

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

    public bool ReConnect()
    {
        if (BasicFunc.SocketConnected(Core) && (Active()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(Core))
            {
                try
                {
                    Connect(Host, Port);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
                return true;
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
