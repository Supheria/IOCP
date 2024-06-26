using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Net;

partial class IocpServerProtocol(IocpServer server)
{
    IocpServer Server { get; } = server;

    Socket? Socket { get; set; } = null;

    DynamicBufferManager ReceiveBuffer { get; } = new(ConstTabel.InitBufferSize);

    AsyncSendBufferManager SendBuffer { get; } = new(ConstTabel.InitBufferSize);

    public SocketInfo SocketInfo { get; } = new();

    public delegate void ServerProtocolEvent();

    public event ServerProtocolEvent? OnClosed;

    object Locker { get; } = new();

    [MemberNotNullWhen(true, nameof(Socket))]
    public bool ProcessAccept(Socket? acceptSocket)
    {
        if (acceptSocket is null)
            return false;
        Socket = acceptSocket;
        // 设置TCP Keep-alive数据包的发送间隔为10秒
        Socket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 1000 * 10, 1000 * 10), null);
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

    ManualResetEvent CloseDone { get; } = new(true);

    public bool Close()
    {
        lock (CloseDone)
        {
            try
            {
                Socket?.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                //Program.Logger.ErrorFormat("CloseClientSocket Disconnect client {0} error, message: {1}", socketInfo, ex.Message);
            }
            Socket?.Close();
            Socket = null;
            ReceiveBuffer.Clear();
            SendBuffer.ClearPacket();
            Dispose();
            SocketInfo.Disconnect();
            OnClosed?.Invoke();
            return true;
        }
    }

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

    /// <summary>
    /// 接收异步事件返回的数据，用于对数据进行缓存和分包
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private void ProcessReceive(SocketAsyncEventArgs receiveArgs)
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

            // 按照长度分包
            // 获取包长度
            var packetLength = BitConverter.ToInt32(ReceiveBuffer.Buffer, 0);
            if (UseNetByteOrder) // 把网络字节顺序转为本地字节顺序
                packetLength = IPAddress.NetworkToHostOrder(packetLength);
            // 最大Buffer异常保护
            if (packetLength > ConstTabel.ReceiveBufferMax || ReceiveBuffer.DataCount > ConstTabel.ReceiveBufferMax)
                goto CLOSE;
            // 收到的数据没有达到包长度，继续接收
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

    /// <summary>
    /// 处理分完包后的数据，把命令和数据分开，并对命令进行解析
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private void HandlePacket(byte[] buffer, int offset, int count)
    {
        if (count < sizeof(int))
            return;
        var commandLength = BitConverter.ToInt32(buffer, offset); //取出命令长度
        var command = Encoding.UTF8.GetString(buffer, offset + sizeof(int), commandLength);
        if (!CommandParser.DecodeProtocolText(command)) //解析命令
            return;
        ProcessCommand(buffer, offset + sizeof(int) + commandLength, count - sizeof(int) - sizeof(int) - commandLength); //处理命令,offset + sizeof(int) + commandLen后面的为数据，数据的长度为count - sizeof(int) - sizeof(int) - length，注意是包的总长度－包长度所占的字节（sizeof(int)）－ 命令长度所占的字节（sizeof(int)） - 命令的长度
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
            CommandSucceed((ProtocolKey.FileSize, FileStream.Length - FileStream.Position));
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
            CommandSucceed(ReadBuffer, 0, count);
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
