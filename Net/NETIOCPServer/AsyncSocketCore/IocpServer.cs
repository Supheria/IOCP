using System.Net;
using System.Net.Sockets;

namespace Net;

public class IocpServer
{
    Socket? Core { get; set; } = null;

    public bool IsStart { get; private set; } = false;

    /// <summary>
    /// 最大支持连接个数
    /// </summary>
    int ParallelCountMax { get; }

    // TODO: remove this and use another way to limit paralle number
    /// <summary>
    /// 限制访问接收连接的线程数，用来控制最大并发数
    /// </summary>
    Semaphore ClientCountMax { get; }

    /// <summary>
    /// Socket最大超时时间，单位为毫秒
    /// </summary>
    public int TimeoutMilliseconds { get; }


    AsyncUserTokenPool UserTokenPool { get; }


    public AsyncUserTokenList UserTokenList { get; } = [];

    private DaemonThread DaemonThread { get; }

    public enum ClientState
    {
        Connect,
        Disconnect,
    }

    public delegate void HandleMessage(string message, ServerFullHandlerProtocol protocol);

    public delegate void ClientNumberChange(ClientState state, AsyncUserToken userToken);

    public delegate void ReceiveClientData(AsyncUserToken userToken, byte[] data);

    public delegate void ParallelRemainChange(int remain);

    public event HandleMessage? OnReceiveMessage;

    public event ClientNumberChange? OnClientNumberChange;

    public event ReceiveClientData? OnReceiveClientData;

    public event ParallelRemainChange? OnParallelRemainChange;

    public IocpServer(int parallelCountMax, int timeoutMilliseconds)
    {
        ParallelCountMax = parallelCountMax;
        //HACK: m_receiveBufferSize = ConstTabel.ReceiveBufferSize;
        UserTokenPool = new(parallelCountMax);
        ClientCountMax = new(parallelCountMax, parallelCountMax);
        //HACK: m_fullHandlerSocketProtocolMgr = new FullHandlerSocketProtocolMgr();//所有新加入的服务端协议，必须在此处实例化
        DaemonThread = new(ProcessDaemon);
        for (int i = 0; i < ParallelCountMax; i++) //按照连接数建立读写对象
        {
            var userToken = new AsyncUserToken(this, ProcessSend);
            userToken.OnClosed += () =>
            {
                //HACK: ClientCountMax.Release();
                UserTokenPool.Push(userToken);
                UserTokenList.Remove(userToken);
                OnClientNumberChange?.Invoke(ClientState.Disconnect, userToken);
                OnParallelRemainChange?.Invoke(UserTokenPool.Count);
            };
            UserTokenPool.Push(userToken);
        }
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    /// <summary>
    /// 守护线程
    /// </summary>
    private void ProcessDaemon()
    {
        UserTokenList.CopyTo(out var userTokenss);
        foreach (var userToken in userTokenss)
        {
            try
            {
                if ((DateTime.Now - userToken.SocketInfo.ActiveTime).Milliseconds > TimeoutMilliseconds) //超时Socket断开
                {
                    lock (userToken)
                        userToken.Close();
                }
            }
            catch (Exception ex)
            {
                //ServerInstance.Logger.ErrorFormat("Daemon thread check timeout socket error, message: {0}", ex.Message);
                //ServerInstance.Logger.Error(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// 设置服务端SOCKET是否延迟，如果保证实时性，请设为true,默认为false
    /// </summary>
    /// <param name="NoDelay"></param>
    /// 
    public void SetNoDelay(bool NoDelay)
    {
        Core.NoDelay = NoDelay;
    }

    public void Start(int port)
    {
        if (IsStart)
        {
            //Program.Logger.InfoFormat("server {0} has started ", localEndPoint.ToString());
            return;
        }
        // 使用0.0.0.0作为绑定IP，则本机所有的IPv4地址都将绑定
        var localEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
        Core = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);            
        Core.Bind(localEndPoint);
        Core.Listen(ParallelCountMax);
        //ServerInstance.Logger.InfoFormat("Start listen socket {0} success", localEndPoint.ToString());
        //for (int i = 0; i < 64; i++) //不能循环投递多次AcceptAsync，会造成只接收8000连接后不接收连接了
        StartAccept(null);
        DaemonThread.Start();
        IsStart = true;
    }

    public void Stop()
    {
        if (!IsStart)
        {
            //ServerInstance.Logger.Info("server {0} has not started yet", localEndPoint.ToString());
            return;
        }
        UserTokenList.CopyTo(out var userTokens);
        foreach (var userToken in userTokens)//双向关闭已存在的连接
            userToken.Close();
        UserTokenList.Clear();
        Core?.Close();
        DaemonThread.Stop();
        IsStart = false;
        //ServerInstance.Logger.Info("Server is Stoped");
    }

    public void StartAccept(SocketAsyncEventArgs? acceptEventArgs)
    {
        if (acceptEventArgs == null)
        {
            acceptEventArgs = new SocketAsyncEventArgs();
            acceptEventArgs.Completed += (sender, acceptArgs) => ProcessAccept(acceptArgs);
        }
        else
        {
            acceptEventArgs.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
        }
        bool willRaiseEvent = Core.AcceptAsync(acceptEventArgs);
        if (!willRaiseEvent)
        {
            ProcessAccept(acceptEventArgs);
        }
    }

    private void ProcessAccept(SocketAsyncEventArgs acceptArgs)
    {
        var userToken = UserTokenPool.Pop();
        if (!userToken.ProcessAccept(acceptArgs.AcceptSocket))
        {
            UserTokenPool.Push(userToken);
            return;
        }
        UserTokenList.Add(userToken);
        //HACK: ClientCountMax.WaitOne(); //获取信号量
        OnParallelRemainChange?.Invoke(UserTokenPool.Count);
        try
        {
            userToken.ReceiveAsync();
            //if (!userToken.AcceptSocket.ReceiveAsync(userToken.ReceiveAsyncArgs))
            //    lock (userToken)
            //        userToken.ProcessReceive();
            OnClientNumberChange?.Invoke(ClientState.Connect, userToken);
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("Accept client {0} error, message: {1}", userToken.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
        if (acceptArgs.SocketError is not SocketError.OperationAborted)
            StartAccept(acceptArgs); //把当前异步事件释放，等待下次连接
    }

    public void HandleReceiveMessage(string message, ServerFullHandlerProtocol protocol)
    {
        OnReceiveMessage?.Invoke(message, protocol);
    }


    private void ProcessSend(SocketAsyncEventArgs sendEventArgs)
    {
        AsyncUserToken userToken = sendEventArgs.UserToken as AsyncUserToken;
        if (userToken.Protocol == null)
            return;
        //userToken.ActiveDateTime = DateTime.Now;
        if (sendEventArgs.SocketError == SocketError.Success)
            userToken.Protocol.SendCompleted(); //调用子类回调函数
        else
        {
            userToken.Close();
        }
    }

    public bool SendAsyncEvent(Socket connectSocket, SocketAsyncEventArgs sendEventArgs, byte[] buffer, int offset, int count)
    {
        if (connectSocket == null)
            return false;
        sendEventArgs.SetBuffer(buffer, offset, count);
        if (!connectSocket.SendAsync(sendEventArgs))
            new Task(() => ProcessSend(sendEventArgs)).Start();
        return true;
    }
}
