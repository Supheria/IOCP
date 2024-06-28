using System.Net;
using System.Net.Sockets;

namespace Net;

public class IocpServer
{
    Socket? Socket { get; set; } = null;

    public bool IsStart { get; private set; } = false;

    int ParallelCountMax { get; }

    // TODO: use another way to limit paralle number

    ServerProtocolPool ProtocolPool { get; }

    public ServerProtocolList ProtocolList { get; } = [];

    private DaemonThread DaemonThread { get; }

    public enum ClientState
    {
        Connect,
        Disconnect,
    }

    public event IocpEventHandler<string>? OnMessage;

    public event IocpEventHandler<ClientState>? OnClientNumberChange;

    public event EventHandler<int>? OnParallelRemainChange;

    public IocpServer(int parallelCountMax)
    {
        ParallelCountMax = parallelCountMax;
        ProtocolPool = new(parallelCountMax);
        DaemonThread = new(ProcessDaemon);
        for (int i = 0; i < ParallelCountMax; i++) //按照连接数建立读写对象
        {
            var protocol = new ServerProtocol();
            protocol.OnClosed += (_) =>
            {
                ProtocolPool.Push(protocol);
                ProtocolList.Remove(protocol);
                OnClientNumberChange?.Invoke(protocol, ClientState.Disconnect);
                OnParallelRemainChange?.Invoke(this, ProtocolPool.Count);
            };
            protocol.OnException += (p, ex) => OnMessage?.Invoke(p, ex.Message);
            protocol.OnFileReceived += (p) => OnMessage?.Invoke(p, $"upload file success at {DateTime.Now}");
            protocol.OnFileSent += (p) => OnMessage?.Invoke(p, $"download file success at {DateTime.Now}");
            protocol.OnMessage += (p, m) => OnMessage?.Invoke(p, m);
            ProtocolPool.Push(protocol);
        }
    }

    /// <summary>
    /// 守护线程
    /// </summary>
    private void ProcessDaemon()
    {
        ProtocolList.CopyTo(out var userTokens);
        var timeout = ConstTabel.TimeoutMilliseconds;
        foreach (var protocol in userTokens)
        {
            try
            {
                var span = DateTime.Now - protocol.SocketInfo.ActiveTime;
                if (span.TotalMilliseconds > timeout) //超时Socket断开
                {
                    lock (protocol)
                        protocol.Close();
                }
            }
            catch (Exception ex)
            {
                //ServerInstance.Logger.ErrorFormat("Daemon thread check timeout socket error, message: {0}", ex.Message);
                //ServerInstance.Logger.Error(ex.StackTrace);
            }
        }
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
        Socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        Socket.Bind(localEndPoint);
        Socket.Listen(ParallelCountMax);
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
        ProtocolList.CopyTo(out var userTokens);
        foreach (var protocol in userTokens)//双向关闭已存在的连接
            protocol.Close();
        ProtocolList.Clear();
        Socket?.Close();
        DaemonThread.Stop();
        IsStart = false;
        //ServerInstance.Logger.Info("Server is Stoped");
    }

    public void StartAccept(SocketAsyncEventArgs? acceptArgs)
    {
        if (acceptArgs == null)
        {
            acceptArgs = new SocketAsyncEventArgs();
            acceptArgs.Completed += (_, args) => ProcessAccept(args);
        }
        else
        {
            acceptArgs.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
        }
        if (Socket is not null && !Socket.AcceptAsync(acceptArgs))
            ProcessAccept(acceptArgs);
    }

    private void ProcessAccept(SocketAsyncEventArgs acceptArgs)
    {
        var protocol = ProtocolPool.Pop();
        if (!protocol.ProcessAccept(acceptArgs.AcceptSocket))
        {
            ProtocolPool.Push(protocol);
            return;
        }
        ProtocolList.Add(protocol);
        OnParallelRemainChange?.Invoke(this, ProtocolPool.Count);
        try
        {
            protocol.ReceiveAsync();
            OnClientNumberChange?.Invoke(protocol, ClientState.Connect);
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("Accept client {0} error, message: {1}", protocol.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
        if (acceptArgs.SocketError is not SocketError.OperationAborted)
            StartAccept(acceptArgs); //把当前异步事件释放，等待下次连接
    }
}
