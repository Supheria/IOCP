using System.Text;

namespace Net;

/// <summary>
/// 异步Socket调用对象，所有的协议处理都从本类继承
/// </summary>
/// <param name="type"></param>
/// <param name="server"></param>
/// <param name="userToken"></param>
public partial class IocpServerProtocol
{
    /// <summary>
    /// 长度是否使用网络字节顺序
    /// </summary>
    public bool UseNetByteOrder { get; set; } = false;

    /// <summary>
    /// 协议解析器，用来解析客户端接收到的命令
    /// </summary>
    protected CommandParser CommandParser { get; } = new();

    /// <summary>
    /// 协议组装器，用来组织服务端返回的命令
    /// </summary>
    protected CommandComposer CommandComposer { get; } = new();

    /// <summary>
    /// 标识是否有发送异步事件
    /// </summary>
    bool IsSendingAsync { get; set; } = false;

    protected void CommandFail(int errorCode, string message)
    {
        CommandComposer.AddFailure(errorCode, message);
        SendCommand();
    }

    protected void CommandSucceed(params (string Key, object value)[] addValues)
    {
        CommandSucceed([], 0, 0, addValues);
    }

    protected void CommandSucceed(byte[] buffer, int offset, int count, params (string Key, object value)[] addValues)
    {
        CommandComposer.AddSuccess();
        foreach (var (key, value) in addValues)
            CommandComposer.AddValue(key, value.ToString() ?? "");
        SendCommand(buffer, offset, count);
    }

    protected void SendCommand()
    {
        SendCommand([], 0, 0);
    }

    protected void SendCommand(byte[] buffer, int offset, int count)
    {
        // 获取命令
        var commandText = CommandComposer.GetProtocolText();
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

    /// <summary>
    /// 不是按包格式下发一个内存块，用于日志这类下发协议
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    // HACK: protected void SendBuffer(byte[] buffer, int offset, int count)
    //{
    //    UserToken.SendBuffer.StartPacket();
    //    UserToken.SendBuffer.DynamicBufferManager.WriteBuffer(buffer, offset, count);
    //    UserToken.SendBuffer.EndPacket();
    //    if (IsSendingAsync)
    //        return;
    //    if (!UserToken.SendBuffer.GetFirstPacket(out var packetOffset, out var packetCount))
    //        return;
    //    IsSendingAsync = true;
    //    UserToken.SendAsync(packetOffset, packetCount);
    //    return;
    //}
}