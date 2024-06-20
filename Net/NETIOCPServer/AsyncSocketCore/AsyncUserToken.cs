using Net;
using System;
using System.Net.Sockets;


namespace Net;

public class AsyncUserToken
{
    public IocpServer Server { get; }

    public Socket? AcceptSocket { get; /*private*/ set; } = null;

    public SocketAsyncEventArgs ReceiveAsyncArgs { get; } = new();

    //HACK: protected byte[] m_asyncReceiveBuffer;
    public SocketAsyncEventArgs SendAsyncArgs { get; } = new();

    public DynamicBufferManager ReceiveBuffer { get; } = new(ConstTabel.InitBufferSize);

    public AsyncSendBufferManager SendBuffer { get; } = new(ConstTabel.InitBufferSize);

    /// <summary>
    /// 协议对象
    /// </summary>
    public IocpServerProtocol? Protocol { get; /*private*/ set; } = null;

    public SocketInfo SocketInfo { get; } = new();

    public delegate void AsyncUserTokenEvent();

    public event AsyncUserTokenEvent? OnClosed;

    public AsyncUserToken(IocpServer server)
    {
        Server = server;
        ReceiveAsyncArgs.UserToken = this;
        SendAsyncArgs.UserToken = this;
        ReceiveAsyncArgs.SetBuffer(new byte[ReceiveBuffer.BufferSize], 0, ReceiveBuffer.BufferSize);
    }
}
