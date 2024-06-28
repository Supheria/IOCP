﻿using System.Net;
using System.Net.Sockets;

namespace Net;

public class IocpServer
{
    Socket? Socket { get; set; } = null;

    public bool IsStart { get; private set; } = false;

    /// <summary>
    /// 最大支持连接个数
    /// </summary>
    int ParallelCountMax { get; }

    // TODO: remove this and use another way to limit paralle number

    /// <summary>
    /// Socket最大超时时间，单位为毫秒
    /// </summary>
    public int TimeoutMilliseconds { get; }

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

    public IocpServer(int parallelCountMax, int timeoutMilliseconds)
    {
        ParallelCountMax = parallelCountMax;
        ProtocolPool = new(parallelCountMax);
        DaemonThread = new(ProcessDaemon);
        for (int i = 0; i < ParallelCountMax; i++) //按照连接数建立读写对象
        {
            var protocol = new ServerProtocol(this);
            protocol.OnClosed += (_) =>
            {
                ProtocolPool.Push(protocol);
                ProtocolList.Remove(protocol);
                OnClientNumberChange?.InvokeAsync(protocol, ClientState.Disconnect);
                OnParallelRemainChange?.InvokeAsync(this, ProtocolPool.Count);
            };
            protocol.OnException += (p, ex) => OnMessage?.Invoke(p, ex.Message);
            protocol.OnFileReceived += (p) => OnMessage?.Invoke(p, $"upload file success at {DateTime.Now}");
            protocol.OnFileSent += (p) => OnMessage?.Invoke(p, $"download file success at {DateTime.Now}");
            protocol.OnMessage += (p, m) => OnMessage?.Invoke(p, m);
            ProtocolPool.Push(protocol);
        }
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    /// <summary>
    /// 守护线程
    /// </summary>
    private void ProcessDaemon()
    {
        ProtocolList.CopyTo(out var userTokenss);
        foreach (var protocol in userTokenss)
        {
            try
            {
                if ((DateTime.Now - protocol.SocketInfo.ActiveTime).Milliseconds > TimeoutMilliseconds) //超时Socket断开
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

    /// <summary>
    /// 设置服务端SOCKET是否延迟，如果保证实时性，请设为true,默认为false
    /// </summary>
    /// <param name="NoDelay"></param>
    /// 
    public void SetNoDelay(bool NoDelay)
    {
        Socket.NoDelay = NoDelay;
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
        OnParallelRemainChange?.InvokeAsync(this, ProtocolPool.Count);
        try
        {
            protocol.ReceiveAsync();
            OnClientNumberChange?.InvokeAsync(protocol, ClientState.Connect);
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("Accept client {0} error, message: {1}", protocol.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
        if (acceptArgs.SocketError is not SocketError.OperationAborted)
            StartAccept(acceptArgs); //把当前异步事件释放，等待下次连接
    }

    /// <summary>
    /// 检测文件是否正在使用中，如果正在使用中则检测是否被上传协议占用，如果占用则关闭,真表示正在使用中，并没有关闭
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public bool CheckFileInUse(string filePath)
    {
        //if (isFileInUse())
        //{
        //    bool result = true;
        //    ProtocolList.CopyTo(out var userTokenss);
        //    foreach (var protocol in userTokenss)
        //    {
        //        if (!filePath.Equals(protocol.FilePath, StringComparison.CurrentCultureIgnoreCase))
        //            continue;
        //        lock (protocol) // AsyncSocketUserToken有多个线程访问
        //        {
        //            protocol.Close();
        //        }
        //        result = false;
        //    }
        //    return result;
        //}
        //return false;
        return isFileInUse();
        bool isFileInUse()
        {
            try
            {
                // 使用共享只读方式打开，可以支持多个客户端同时访问一个文件。
                using var _ = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
