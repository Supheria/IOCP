using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace Net;

partial class IocpServerProtocol
{
    IocpServer Server { get; }

    Socket? Socket { get; set; } = null;

    SocketAsyncEventArgs ReceiveAsyncArgs { get; } = new();

    SocketAsyncEventArgs SendAsyncArgs { get; } = new();
    
    DynamicBufferManager ReceiveBuffer { get; } = new(ConstTabel.InitBufferSize);
    
    AsyncSendBufferManager SendBuffer { get; } = new(ConstTabel.InitBufferSize);

    public SocketInfo SocketInfo { get; } = new();

    public delegate void ServerProtocolEvent();

    public event ServerProtocolEvent? OnClosed;

    object Locker { get; } = new();

    public IocpServerProtocol(IocpServer server)
    {
        Server = server;
        ReceiveAsyncArgs.SetBuffer(new byte[ReceiveBuffer.BufferSize], 0, ReceiveBuffer.BufferSize);
        ReceiveAsyncArgs.Completed += (_, _) => ProcessReceive();
        SendAsyncArgs.Completed += (_, _) => ProcessSend();
    }

    [MemberNotNullWhen(true, nameof(Socket))]
    public bool ProcessAccept(Socket? acceptSocket)
    {
        if (acceptSocket is null)
            return false;
        Socket = acceptSocket;
        // 设置TCP Keep-alive数据包的发送间隔为10秒
        Socket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 1000 * 10, 1000 * 10), null);
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
    private static byte[] KeepAlive(int onOff, int keepAliveTime, int keepAliveInterval)
    {
        byte[] buffer = new byte[12];
        BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
        BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4);
        BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8);
        return buffer;
    }

    public bool Close()
    {
        if (Socket is null)
            return false;
        try
        {
            Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception ex)
        {
            //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
        }
        Socket.Close();
        Socket = null;
        ReceiveBuffer.Clear(ReceiveBuffer.DataCount);
        SendBuffer.ClearPacket();
        Dispose();
        SocketInfo.Disconnect();
        OnClosed?.Invoke();
        return true;
    }

    public void ReceiveAsync()
    {
        if (Socket is not null && !Socket.ReceiveAsync(ReceiveAsyncArgs))
        {
            lock (Locker)
                ProcessReceive();
        }
    }

    public void ProcessReceive()
    {
        if (Socket is null)
            return;
        if (ReceiveAsyncArgs.Buffer is null || ReceiveAsyncArgs.BytesTransferred <= 0 || ReceiveAsyncArgs.SocketError is not SocketError.Success)
            goto CLOSE;
        var offset = ReceiveAsyncArgs.Offset;
        var count = ReceiveAsyncArgs.BytesTransferred;
        SocketInfo.Active();
        if (count > 0 && !ProcessReceive(ReceiveAsyncArgs.Buffer, offset, count))
            goto CLOSE;
        if (!Socket.ReceiveAsync(ReceiveAsyncArgs))
            ProcessReceive();
        return;
    CLOSE:
        // 接收数据长度为0或者SocketError 不等于 SocketError.Success表示socket已经断开，所以服务端执行断开清理工作
        Close();
    }

    public void SendAsync(int offset, int count)
    {
        if (Socket is null)
            return;
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
            Close();
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
        else
            SendCallback();
    }

    /// <summary>
    /// 发送回调函数，用于连续下发数据
    /// </summary>
    /// <returns></returns>
    private void SendCallback()
    {
        if (FileStream is null)
            return;
        if (IsSendingFile) // 发送文件头
        {
            CommandComposer.Clear();
            CommandComposer.AddResponse();
            CommandComposer.AddCommand(ProtocolKey.SendFile);
            _ = CommandSucceed((ProtocolKey.FileSize, FileStream.Length - FileStream.Position));
            IsSendingFile = false;
            return;
        }
        if (IsReceivingFile)
            return;
        // 没有接收文件时
        // 发送具体数据,加FileStream.CanSeek是防止上传文件结束后，文件流被释放而出错
        if (FileStream.CanSeek && FileStream.Position < FileStream.Length)
        {
            CommandComposer.Clear();
            CommandComposer.AddResponse();
            CommandComposer.AddCommand(ProtocolKey.Data);
            ReadBuffer ??= new byte[PacketSize];
            // 避免多次申请内存
            if (ReadBuffer.Length < PacketSize)
                ReadBuffer = new byte[PacketSize];
            var count = FileStream.Read(ReadBuffer, 0, PacketSize);
            _ = CommandSucceed(ReadBuffer, 0, count);
            return;
        }
        // 发送完成
        //ServerInstance.Logger.Info("End Upload file: " + FilePath);
        FileStream.Close();
        FileStream = null;
        FilePath = "";
        IsSendingFile = false;
    }
}
