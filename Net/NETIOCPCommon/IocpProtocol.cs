using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Net;

public abstract class IocpProtocol : IDisposable
{
    public delegate void HandleEvent(IocpProtocol protocol);

    protected Socket? Socket { get; set; } = null;

    public SocketInfo SocketInfo { get; } = new();
    /// <summary>
    /// 使用网络字节顺序
    /// </summary>
    public bool UseNetByteOrder { get; set; } = false;

    protected DynamicBufferManager ReceiveBuffer { get; } = new(ConstTabel.InitBufferSize);

    protected AsyncSendBufferManager SendBuffer { get; } = new(ConstTabel.InitBufferSize);

    protected CommandComposer CommandComposer { get; } = new();

    //protected CommandParser CommandParser { get; } = new();

    public string FilePath { get; protected set; } = "";

    protected byte[]? ReadBuffer { get; set; } = null;

    protected FileStream? FileStream { get; set; } = null;

    bool IsSendingAsync { get; set; } = false;

    protected bool IsLogin { get; set; } = false;

    public UserInfo UserInfo { get; } = new();

    object CloseLocker { get; } = new();

    public event HandleEvent? OnClosed;

    public void Close() => Dispose();

    public void Dispose()
    {
        lock (CloseLocker)
        {
            if (Socket is null)
                return;
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
            ReceiveBuffer.Clear();
            SendBuffer.ClearPacket();
            FilePath = "";
            FileStream?.Close();
            FileStream = null;
            SocketInfo.Disconnect();
            GC.SuppressFinalize(this);
            new Task(() => OnClosed?.Invoke(this)).Start();
        }
    }

    public void ReceiveAsync()
    {
        var receiveArgs = new SocketAsyncEventArgs();
        receiveArgs.SetBuffer(new byte[ReceiveBuffer.BufferSize], 0, ReceiveBuffer.BufferSize);
        receiveArgs.Completed += (_, args) => ProcessReceive(args);
        if (Socket is not null && !Socket.ReceiveAsync(receiveArgs))
        {
            lock (Socket)
                ProcessReceive(receiveArgs);
        }
    }

    private void ProcessReceive(SocketAsyncEventArgs receiveArgs)
    {
        if (Socket is null ||
            receiveArgs.Buffer is null ||
            receiveArgs.BytesTransferred <= 0 ||
            receiveArgs.SocketError is not SocketError.Success)
            goto CLOSE;
        SocketInfo.Active();
        ReceiveBuffer.WriteBuffer(receiveArgs.Buffer!, receiveArgs.Offset, receiveArgs.BytesTransferred);
        // 按照长度分包
        // 小于四个字节表示包头未完全接收，继续接收
        while (ReceiveBuffer.DataCount > sizeof(int))
        {
            var packetLength = BitConverter.ToInt32(ReceiveBuffer.Buffer, 0);
            if (UseNetByteOrder)
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

    private void HandlePacket(byte[] buffer, int offset, int count)
    {
        if (count < sizeof(int))
            return;
        var commandLength = BitConverter.ToInt32(buffer, offset); //取出命令长度
        var command = Encoding.UTF8.GetString(buffer, offset + sizeof(int), commandLength);
        var commandParser = CommandParser.Parse(command);
        ProcessCommand(commandParser, buffer, offset + sizeof(int) + commandLength, count - sizeof(int) - sizeof(int) - commandLength); //处理命令,offset + sizeof(int) + commandLen后面的为数据，数据的长度为count - sizeof(int) - sizeof(int) - length，注意是包的总长度－包长度所占的字节（sizeof(int)）－ 命令长度所占的字节（sizeof(int)） - 命令的长度
    }

    protected abstract void ProcessCommand(CommandParser commandParser, byte[] buffer, int offset, int count);

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

    private void ProcessSend(SocketAsyncEventArgs sendArgs)
    {
        SocketInfo.Active();
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

    protected virtual void SendCallback()
    {

    }

    public void SendCommand()
    {
        SendCommand([], 0, 0);
    }

    protected void SendCommand(byte[] buffer, int offset, int count)
    {
        // 获取命令
        var commandText = CommandComposer.GetCommand();
        // 获取命令的字节数组
        var bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
        // 获取总大小(4个字节的包总长度+4个字节的命令长度+命令字节数组的长度+数据的字节数组长度)
        int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length + count;
        SendBuffer.StartPacket();
        SendBuffer.DynamicBufferManager.WriteInt(totalLength, false); // 写入总大小
        SendBuffer.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); // 写入命令大小
        SendBuffer.DynamicBufferManager.WriteBuffer(bufferUTF8); // 写入命令内容
        SendBuffer.DynamicBufferManager.WriteBuffer(buffer, offset, count); // 写入二进制数据
        SendBuffer.EndPacket();
        if (IsSendingAsync)
            return;
        if (!SendBuffer.GetFirstPacket(out var packetOffset, out var packetCount))
            return;
        IsSendingAsync = true;
        SendAsync(SendBuffer.DynamicBufferManager.Buffer, packetOffset, packetCount);
        return;
    }
}
