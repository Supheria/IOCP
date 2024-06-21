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

    Action<SocketAsyncEventArgs> ProcessReceiveOperation { get; }

    Action<SocketAsyncEventArgs> ProcessSendOperation { get; }

    public AsyncUserToken(IocpServer server, Action<SocketAsyncEventArgs> processReceiveOperation, Action<SocketAsyncEventArgs> processSendOperation)
    {
        Server = server;
        ReceiveAsyncArgs.UserToken = this;
        SendAsyncArgs.UserToken = this;
        ReceiveAsyncArgs.SetBuffer(new byte[ReceiveBuffer.BufferSize], 0, ReceiveBuffer.BufferSize);
        ProcessReceiveOperation = processReceiveOperation;
        ProcessSendOperation = processSendOperation;
        ReceiveAsyncArgs.Completed += CompleteIO;
        SendAsyncArgs.Completed += CompleteIO;
    }

    void CompleteIO(object? sender, SocketAsyncEventArgs asyncEventArgs)
    {
        AsyncUserToken userToken = asyncEventArgs.UserToken as AsyncUserToken;
        //userToken.ActiveDateTime = DateTime.Now;
        try
        {
            lock (userToken)
            {
                if (asyncEventArgs.LastOperation == SocketAsyncOperation.Receive)
                    ProcessReceiveOperation(asyncEventArgs);
                else if (asyncEventArgs.LastOperation == SocketAsyncOperation.Send)
                    ProcessSendOperation(asyncEventArgs);
                else
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }
        catch (Exception E)
        {
            //ServerInstance.Logger.ErrorFormat("CompleteIO {0} error, message: {1}", userToken.AcceptSocket, E.Message);
            //ServerInstance.Logger.Error(E.StackTrace);
        }
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




    public void ReceiveAsync()
    {
        if (AcceptSocket is not null && !AcceptSocket.ReceiveAsync(ReceiveAsyncArgs))
            ProcessReceive(ReceiveAsyncArgs);
    }

    //public void ProcessReceive()
    //{
    //    ProcessReceive(ReceiveAsyncArgs);
    //}

    public static void ProcessReceive(SocketAsyncEventArgs receiveArgs)
    {
        if (receiveArgs.UserToken is not AsyncUserToken userToken)
            return;
        if (userToken.AcceptSocket is null)
            return;
        if (userToken.ReceiveAsyncArgs.Buffer is null || userToken.ReceiveAsyncArgs.BytesTransferred <= 0 || userToken.ReceiveAsyncArgs.SocketError is not SocketError.Success)
            goto CLOSE;
        userToken.SocketInfo.Active();
        if (!userToken.BuildProtocol())
            goto CLOSE;
        var offset = userToken.ReceiveAsyncArgs.Offset + 1;
        var count = userToken.ReceiveAsyncArgs.BytesTransferred - 1;
        // 处理接收数据
        if (count > 0 && !userToken.Protocol.ProcessReceive(userToken.ReceiveAsyncArgs.Buffer, offset, count))
            goto CLOSE;
        userToken.ReceiveAsync();
        return;
    CLOSE:
        userToken.Close();
    }

    public bool BuildProtocol()
    {
        if (AcceptSocket is null || ReceiveAsyncArgs.Buffer is null)
            return false;
        var protocolType = (IocpProtocolTypes)ReceiveAsyncArgs.Buffer[ReceiveAsyncArgs.Offset];
        Protocol = protocolType switch
        {
            IocpProtocolTypes.FullHandler => new ServerFullHandlerProtocol(Server, this),
            _ => null
        };
        if (Protocol is not null)
        {
            //ServerInstance.Logger.InfoFormat("Building socket invoke element {0}.Local Address: {1}, Remote Address: {2}",
            //    userToken.Protocol, userToken.AcceptSocket.LocalEndPoint, userToken.AcceptSocket.RemoteEndPoint);
            return true;
        }
        return false;
    }

    private void ProcessSend(SocketAsyncEventArgs sendArgs)
    {
        if (sendArgs.UserToken is not AsyncUserToken userToken)
            return;
        if (userToken.Protocol is null)
            return;
        SocketInfo.Active();
        // 调用子类回调函数
        //if (sendArgs.SocketError is SocketError.Success)
        //    userToken.Protocol.ProcessSend();
        //else
        //    userToken.Close();
    }
}