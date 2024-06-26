using System.Text;

namespace Net;

public partial class IocpClientProtocol
{
    protected string Host { get; private set; } = "";

    protected int Port { get; private set; } = 0;

    /// <summary>
    /// 长度是否使用网络字节顺序
    /// </summary>
    public bool UseNetByteOrder { get; set; } = false;

    /// <summary>
    /// 协议组装器，用来组装往外发送的命令
    /// </summary>
    protected CommandComposer CommandComposer { get; } = new();

    /// <summary>
    /// 收到数据的解析器，用于解析返回的内容
    /// </summary>
    protected CommandParser CommandParser { get; } = new();

    /// <summary>
    /// 接收数据的缓存
    /// </summary>
    protected DynamicBufferManager ReceiveBuffer { get; } = new(ConstTabel.ReceiveBufferSize);

    /// <summary>
    /// 发送数据的缓存，统一写到内存中，调用一次发送
    /// </summary>
    protected AsyncSendBufferManager SendBuffer { get; } = new(ConstTabel.ReceiveBufferSize);

    public void SendCommand()
    {
        SendCommand([], 0, 0);
    }

    public void SendCommand(byte[] buffer, int offset, int count)
    {
        string commandText = CommandComposer.GetProtocolText();
        byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
        int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length + count; //获取总大小
        //SendBuffer.Clear();
        SendBuffer.StartPacket();
        SendBuffer.DynamicBufferManager.WriteInt(totalLength, false); //写入总大小
        SendBuffer.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); //写入命令大小
        SendBuffer.DynamicBufferManager.WriteBuffer(bufferUTF8); //写入命令内容
        SendBuffer.DynamicBufferManager.WriteBuffer(buffer, offset, count); //写入二进制数据
        SendBuffer.EndPacket();
        //SendAsync(Core, SendBuffer.Buffer, 0, SendBuffer.DataCount, SocketFlags.None);
        if (IsSendingAsync)
            return;
        if (!SendBuffer.GetFirstPacket(out var packetOffset, out var packetCount))
            return;
        IsSendingAsync = true;
        SendAsync(SendBuffer.DynamicBufferManager.Buffer, packetOffset, packetCount);
        return;
    }
}
