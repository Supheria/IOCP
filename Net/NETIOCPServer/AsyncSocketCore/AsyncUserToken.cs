﻿using Net;
using System;
using System.Diagnostics.CodeAnalysis;
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


    [MemberNotNullWhen(true, nameof(AcceptSocket))]
    public bool ProcessAccept(Socket? acceptSocket)
    {
        if (acceptSocket is null)
            return false;
        AcceptSocket = acceptSocket;
        // 设置TCP Keep-alive数据包的发送间隔为10秒
        AcceptSocket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 1000 * 10, 1000 * 10), null);
        ReceiveAsyncArgs.AcceptSocket = acceptSocket;
        SendAsyncArgs.AcceptSocket = acceptSocket;
        SocketInfo.Connect(acceptSocket);
        return true;
    }

    /// <summary>
    /// keep alive 设置
    /// </summary>
    /// <param name="onOff">是否开启（1为开，0为关）</param>
    /// <param name="keepAliveTime">当开启keep-alive后，经过多长时间（ms）开启侦测</param>
    /// <param name="keepAliveInterval">多长时间侦测一次（ms）</param>
    /// <returns>keep alive 输入参数</returns>
    private byte[] KeepAlive(int onOff, int keepAliveTime, int keepAliveInterval)
    {
        byte[] buffer = new byte[12];
        BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
        BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4);
        BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8);
        return buffer;
    }

    public bool Close()
    {
        if (AcceptSocket is null)
            return false;
        try
        {
            AcceptSocket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
        }
        AcceptSocket.Close();
        AcceptSocket = null;
        ReceiveAsyncArgs.AcceptSocket = null;
        SendAsyncArgs.AcceptSocket = null;
        ReceiveBuffer.Clear(ReceiveBuffer.DataCount);
        SendBuffer.ClearPacket();
        Protocol?.Dispose();
        Protocol = null;
        SocketInfo.Disconnect();
        OnClosed?.Invoke();
        return true;
    }
}
