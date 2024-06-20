using Net;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Net;

public class IocpServer
{
    Socket? Core { get; set; } = null;

    public bool IsStart { get; private set; } = false;

    /// <summary>
    /// 最大支持连接个数
    /// </summary>
    int ParalleCountMax { get; }

    //private int m_receiveBufferSize; //每个连接接收缓存大小

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

    //HACK: private FullHandlerSocketProtocolMgr m_fullHandlerSocketProtocolMgr;
    //HACK: public FullHandlerSocketProtocolMgr FullHandlerSocketProtocolMgr { get { return m_fullHandlerSocketProtocolMgr; } }

    private DaemonThread DaemonThread { get; }

    public enum ClientState
    {
        Connect,
        Disconnect,
    }

    public delegate void HandleMessage(string message, ServerFullHandlerProtocol protocol);

    public delegate void ClientNumberChange(ClientState state, AsyncUserToken userToken);

    public delegate void ReceiveClientData(AsyncUserToken userToken, byte[] data);

    public delegate void ParalleRemainChange(int remain);

    public event HandleMessage? OnReceiveMessage;

    public event ClientNumberChange? OnClientNumberChange;

    public event ReceiveClientData? OnReceiveClientData;

    public event ParalleRemainChange? OnParalleRemainChange;

    public IocpServer(int paralleCountMax, int timeoutMilliseconds)
    {
        ParalleCountMax = paralleCountMax;
        //HACK: m_receiveBufferSize = ConstTabel.ReceiveBufferSize;
        UserTokenPool = new(paralleCountMax);
        ClientCountMax = new(paralleCountMax, paralleCountMax);
        //HACK: m_fullHandlerSocketProtocolMgr = new FullHandlerSocketProtocolMgr();//所有新加入的服务端协议，必须在此处实例化
        DaemonThread = new(ProcessDaemon);
        for (int i = 0; i < ParalleCountMax; i++) //按照连接数建立读写对象
        {
            var userToken = new AsyncUserToken(this);
            userToken.ReceiveAsyncArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);//每一个连接会话绑定一个接收完成事件
            userToken.SendAsyncArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);//每一个连接会话绑定一个发送完成事件
            userToken.OnClosed += () =>
            {
                ClientCountMax.Release();
                UserTokenPool.Push(userToken);
                UserTokenList.Remove(userToken);
                OnClientNumberChange?.Invoke(ClientState.Disconnect, userToken);
                OnParalleRemainChange?.Invoke(UserTokenPool.Count);
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
                        //userToken.Close();
                        CloseClientSocket(userToken);
                }
            }
            catch (Exception ex)
            {
                //ServerInstance.Logger.ErrorFormat("Daemon thread check timeout socket error, message: {0}", ex.Message);
                //ServerInstance.Logger.Error(ex.StackTrace);
            }
        }
    }

    // HACK: remove
    //public void Init()
    //{
    //    AsyncUserToken userToken;
    //    for (int i = 0; i < ParalleCountMax; i++) //按照连接数建立读写对象
    //    {
    //        userToken = new AsyncSocketUserToken(m_receiveBufferSize);
    //        userToken.ReceiveAsyncArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);//每一个连接会话绑定一个接收完成事件
    //        userToken.SendAsyncArgs.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);//每一个连接会话绑定一个发送完成事件
    //        UserTokenPool.Push(userToken);
    //    }
    //}
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
        Core.Listen(ParalleCountMax);
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
            //userToken.Close();
            CloseClientSocket(userToken);
        UserTokenList.Clear();
        Core?.Close();
        DaemonThread.Stop();
        IsStart = false;
        //ServerInstance.Logger.Info("Server is Stoped");
    }

    public void StartAccept(SocketAsyncEventArgs acceptEventArgs)
    {
        if (acceptEventArgs == null)
        {
            acceptEventArgs = new SocketAsyncEventArgs();
            //acceptArgs.Completed += new EventHandler<SocketAsyncEventArgs>(AcceptEventArg_Completed);
            acceptEventArgs.Completed += (sender, acceptArgs) => ProcessAccept(acceptArgs);
        }
        else
        {
            acceptEventArgs.AcceptSocket = null; //释放上次绑定的Socket，等待下一个Socket连接
        }

        //ClientCountMax.WaitOne(); //获取信号量
        bool willRaiseEvent = Core.AcceptAsync(acceptEventArgs);
        if (!willRaiseEvent)
        {
            ProcessAccept(acceptEventArgs);
        }
    }

    //void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs acceptArgs)
    //{
    //    try
    //    {
    //        ProcessAccept(acceptArgs);
    //    }
    //    catch (Exception E)
    //    {
    //        //ServerInstance.Logger.ErrorFormat("Accept client {0} error, message: {1}", acceptArgs.AcceptSocket, E.Message);
    //        //ServerInstance.Logger.Error(E.StackTrace);
    //    }
    //}

    private void ProcessAccept(SocketAsyncEventArgs acceptArgs)
    {
        //ServerInstance.Logger.InfoFormat("Client connection accepted. Local Address: {0}, Remote Address: {1}",
        //    acceptArgs.AcceptSocket.LocalEndPoint, acceptArgs.AcceptSocket.RemoteEndPoint);
        var userToken = UserTokenPool.Pop();
        if (!userToken.ProcessAccept(acceptArgs.AcceptSocket))
        {
            UserTokenPool.Push(userToken);
            return;
        }
        UserTokenList.Add(userToken);
        ClientCountMax.WaitOne(); //获取信号量
        OnParalleRemainChange?.Invoke(UserTokenPool.Count);
        //AsyncUserToken userToken = UserTokenPool.Pop();            
        //UserTokenList.Add(userToken); //添加到正在连接列表
        //userToken.AcceptSocket = acceptArgs.AcceptSocket;
        //userToken.AcceptSocket.IOControl(IOControlCode.KeepAliveValues, keepAlive(1, 1000 * 10 , 1000 * 10), null);//设置TCP Keep-alive数据包的发送间隔为10秒
        ////userToken.ConnectDateTime = DateTime.Now;
        try
        {
            if (!userToken.AcceptSocket.ReceiveAsync(userToken.ReceiveAsyncArgs))
                lock (userToken)
                    ProcessReceive(userToken.ReceiveAsyncArgs);
            OnClientNumberChange?.Invoke(ClientState.Connect, userToken);
            //bool willRaiseEvent = userToken.AcceptSocket.ReceiveAsync(userToken.ReceiveAsyncArgs); //投递接收请求
            //if (!willRaiseEvent)
            //{
            //    lock (userToken)
            //    {
            //        ProcessReceive(userToken.ReceiveAsyncArgs);
            //    }
            //}
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("Accept client {0} error, message: {1}", userToken.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
        if (acceptArgs.SocketError is not SocketError.OperationAborted)
            StartAccept(acceptArgs); //把当前异步事件释放，等待下次连接
    }
    /// <summary>
    /// keep alive 设置
    /// </summary>
    /// <param name="onOff">是否开启（1为开，0为关）</param>
    /// <param name="keepAliveTime">当开启keep-alive后，经过多长时间（ms）开启侦测</param>
    /// <param name="keepAliveInterval">多长时间侦测一次（ms）</param>
    /// <returns>keep alive 输入参数</returns>
    private byte[] keepAlive(int onOff,int keepAliveTime,int keepAliveInterval)
    {
        byte[] buffer = new byte[12];
        BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
        BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4);
        BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8);
        return buffer;
    }

    void IO_Completed(object sender, SocketAsyncEventArgs asyncEventArgs)
    {
        AsyncUserToken userToken = asyncEventArgs.UserToken as AsyncUserToken;
        //userToken.ActiveDateTime = DateTime.Now;
        try
        {
            lock (userToken)
            {
                if (asyncEventArgs.LastOperation == SocketAsyncOperation.Receive)
                    ProcessReceive(asyncEventArgs);
                else if (asyncEventArgs.LastOperation == SocketAsyncOperation.Send)
                    ProcessSend(asyncEventArgs);
                else
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("IO_Completed {0} error, message: {1}", userToken.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
    }

    public void HandleReceiveMessage(string message, ServerFullHandlerProtocol protocol)
    {
        OnReceiveMessage?.Invoke(message, protocol);
    }

    private void ProcessReceive(SocketAsyncEventArgs receiveEventArgs)
    {
        AsyncUserToken userToken = receiveEventArgs.UserToken as AsyncUserToken;
        if (userToken.AcceptSocket == null)
            return;
        //userToken.ActiveDateTime = DateTime.Now;
        if (userToken.ReceiveAsyncArgs.BytesTransferred > 0 && userToken.ReceiveAsyncArgs.SocketError == SocketError.Success)
        {
            int offset = userToken.ReceiveAsyncArgs.Offset;
            int count = userToken.ReceiveAsyncArgs.BytesTransferred;
            if ((userToken.Protocol == null) & (userToken.AcceptSocket != null)) //存在Socket对象，并且没有绑定协议对象，则进行协议对象绑定
            {
                BuildingSocketInvokeElement(userToken);
                offset = offset + 1;
                count = count - 1;
            }
            if (userToken.Protocol == null) //如果没有解析对象，提示非法连接并关闭连接
            {
                //ServerInstance.Logger.WarnFormat("Illegal client connection. Local Address: {0}, Remote Address: {1}", userToken.AcceptSocket.LocalEndPoint,
                //userToken.AcceptSocket.RemoteEndPoint);
                CloseClientSocket(userToken);
            }
            else
            {
                if (count > 0) //处理接收数据
                {
                    if (!userToken.Protocol.ProcessReceive(userToken.ReceiveAsyncArgs.Buffer, offset, count))
                    { //如果处理数据返回失败，则断开连接
                        CloseClientSocket(userToken);
                    }
                    else //否则投递下次接收数据请求
                    {
                        bool willRaiseEvent = userToken.AcceptSocket.ReceiveAsync(userToken.ReceiveAsyncArgs); //投递接收请求
                        if (!willRaiseEvent)
                            ProcessReceive(userToken.ReceiveAsyncArgs);
                    }
                }
                else
                {
                    bool willRaiseEvent = userToken.AcceptSocket.ReceiveAsync(userToken.ReceiveAsyncArgs); //投递接收请求
                    if (!willRaiseEvent)
                        ProcessReceive(userToken.ReceiveAsyncArgs);
                }
            }
        }
        else
        {
            CloseClientSocket(userToken); //接收数据长度为0或者SocketError 不等于 SocketError.Success表示socket已经断开，所以服务端执行断开清理工作
        }
    }

    private void BuildingSocketInvokeElement(AsyncUserToken userToken)
    {
        byte flag = userToken.ReceiveAsyncArgs.Buffer[userToken.ReceiveAsyncArgs.Offset];
        if (flag == (byte)IocpProtocolTypes.FullHandler)
            userToken.Protocol = new ServerFullHandlerProtocol(this, userToken);//全功能处理协议                   
        if (userToken.Protocol != null)
        {
            //ServerInstance.Logger.InfoFormat("Building socket invoke element {0}.Local Address: {1}, Remote Address: {2}",
            //    userToken.Protocol, userToken.AcceptSocket.LocalEndPoint, userToken.AcceptSocket.RemoteEndPoint);
        }
    }

    private bool ProcessSend(SocketAsyncEventArgs sendEventArgs)
    {
        AsyncUserToken userToken = sendEventArgs.UserToken as AsyncUserToken;
        if (userToken.Protocol == null)
            return false;
        //userToken.ActiveDateTime = DateTime.Now;
        if (sendEventArgs.SocketError == SocketError.Success)
            return userToken.Protocol.SendCompleted(); //调用子类回调函数
        else
        {
            CloseClientSocket(userToken);
            return false;
        }
    }

    public bool SendAsyncEvent(Socket connectSocket, SocketAsyncEventArgs sendEventArgs, byte[] buffer, int offset, int count)
    {
        if (connectSocket == null)
            return false;
        sendEventArgs.SetBuffer(buffer, offset, count);
        bool willRaiseEvent = connectSocket.SendAsync(sendEventArgs);
        if (!willRaiseEvent)
        {
            //return ProcessSend(sendEventArgs);
            //connectSocket.BeginSend(buffer, offset, count, SocketFlags.None, (r) =>
            //{
            //    var socket = r.AsyncState as Socket;
            //    socket?.EndSend(r);
            //}, connectSocket);
            new Task(() => ProcessSend(sendEventArgs)).Start();
            return true;
        }
        else
            return true;
    }

    public static void CloseClientSocket(AsyncUserToken userToken)
    {
        //if (userToken.AcceptSocket == null)
        //    return;
        //string socketInfo = string.Format("Local Address: {0} Remote Address: {1}", userToken.AcceptSocket.LocalEndPoint,
        //    userToken.AcceptSocket.RemoteEndPoint);
        //ServerInstance.Logger.InfoFormat("Client connection disconnected. {0}", socketInfo);
        //try
        //{
        //    userToken.AcceptSocket.Shutdown(SocketShutdown.Both);
        //}
        //catch (Exception E)
        //{
        //    //ServerInstance.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, E.Message);
        //}
        userToken.Close();
        //userToken.AcceptSocket.Close();
        //userToken.AcceptSocket = null; //释放引用，并清理缓存，包括释放协议对象等资源

        //ClientCountMax.Release();
        //UserTokenPool.Push(userToken);
        //UserTokenList.Remove(userToken);
        //OnClientNumberChange?.Invoke(ClientState.Disconnect, userToken);
    }
    public void Close()
    {
        UserTokenList.CopyTo(out var userTokens);
        foreach(var userToken in userTokens)//双向关闭已存在的连接
        {
            CloseClientSocket(userToken);
        }
        Core.Close();
        DaemonThread.Stop();
        //ServerInstance.Logger.Info("Server is Stoped");
    }
}
