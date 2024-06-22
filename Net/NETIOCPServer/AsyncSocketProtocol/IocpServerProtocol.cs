using System.Net;
using System.Text;

namespace Net;

/// <summary>
/// 异步Socket调用对象，所有的协议处理都从本类继承
/// </summary>
/// <param name="type"></param>
/// <param name="server"></param>
/// <param name="userToken"></param>
public abstract partial class IocpServerProtocol(IocpProtocolTypes type, IocpServer server, AsyncUserToken userToken) : IDisposable
{
    public IocpProtocolTypes Type { get; } = type;

    //HACK: protected IocpServer Server { get; } = server;

    public AsyncUserToken UserToken { get; } = userToken;

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
    protected bool IsSendingAsync { get; set; } = false;

    public DateTime ConnectTime { get; } = DateTime.UtcNow;

    public DateTime ActiveTime { get; protected set; } = DateTime.UtcNow;

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 接收异步事件返回的数据，用于对数据进行缓存和分包
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public virtual bool ProcessReceive(byte[] buffer, int offset, int count)
    {
        //HACK: ActiveTime = DateTime.UtcNow;
        UserToken.ReceiveBuffer.WriteBuffer(buffer, offset, count);
        while (UserToken.ReceiveBuffer.DataCount > sizeof(int))
        {
            // 按照长度分包
            // 获取包长度
            int packetLength = BitConverter.ToInt32(UserToken.ReceiveBuffer.Buffer, 0);
            if (UseNetByteOrder) // 把网络字节顺序转为本地字节顺序
                packetLength = IPAddress.NetworkToHostOrder(packetLength);
            // 最大Buffer异常保护
            if ((packetLength > 10 * 1024 * 1024) | (UserToken.ReceiveBuffer.DataCount > 10 * 1024 * 1024))
                return false;
            // 收到的数据没有达到包长度，继续接收
            if (UserToken.ReceiveBuffer.DataCount < packetLength)
                return true;
            if (HandlePacket(UserToken.ReceiveBuffer.Buffer, sizeof(int), packetLength))
                UserToken.ReceiveBuffer.Clear(packetLength); // 从缓存中清理
            else
                return false;
        }
        return true;
    }

    /// <summary>
    /// 处理分完包后的数据，把命令和数据分开，并对命令进行解析
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    protected virtual bool HandlePacket(byte[] buffer, int offset, int count)
    {
        if (count < sizeof(int))
            return false;
        var length = BitConverter.ToInt32(buffer, offset); //取出命令长度
        var command = Encoding.UTF8.GetString(buffer, offset + sizeof(int), length);
        if (!CommandParser.DecodeProtocolText(command)) //解析命令
            return false;
        return ProcessCommand(buffer, offset + sizeof(int) + length, count - sizeof(int) - sizeof(int) - length); //处理命令,offset + sizeof(int) + commandLen后面的为数据，数据的长度为count - sizeof(int) - sizeof(int) - length，注意是包的总长度－包长度所占的字节（sizeof(int)）－ 命令长度所占的字节（sizeof(int)） - 命令的长度
    }

    public virtual void ProcessSend()
    {
        //HACK: ActiveTime = DateTime.UtcNow;
        IsSendingAsync = false;
        UserToken.SendBuffer.ClearFirstPacket(); //清除已发送的包
        if (UserToken.SendBuffer.GetFirstPacket(out var offset, out var count))
        {
            IsSendingAsync = true;
            UserToken.SendAsync(offset, count);
        }
    }

    /// <summary>
    /// 处理具体命令，子类从这个方法继承，buffer是收到的数据
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    protected abstract bool ProcessCommand(byte[] buffer, int offset, int count);

    protected bool CommandFail(int errorCode, string message)
    {
        CommandComposer.AddFailure(errorCode, message);
        SendCommand();
        return false;
    }

    protected bool CommandSucceed(params (string Key, object value)[] addValues)
    {
        return CommandSucceed([], 0, 0, addValues);
    }

    protected bool CommandSucceed(byte[] buffer, int offset, int count, params (string Key, object value)[] addValues)
    {
        CommandComposer.AddSuccess();
        foreach (var (key, value) in addValues)
            CommandComposer.AddValue(key, value.ToString() ?? "");
        SendCommand(buffer, offset, count);
        return true;
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
        UserToken.SendBuffer.StartPacket();
        UserToken.SendBuffer.DynamicBufferManager.WriteInt(totalLength, false); // 写入总大小
        UserToken.SendBuffer.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); // 写入命令大小
        UserToken.SendBuffer.DynamicBufferManager.WriteBuffer(bufferUTF8); // 写入命令内容
        UserToken.SendBuffer.DynamicBufferManager.WriteBuffer(buffer, offset, count); // 写入二进制数据
        UserToken.SendBuffer.EndPacket();
        if (IsSendingAsync)
            return;
        if (!UserToken.SendBuffer.GetFirstPacket(out var packetOffset, out var packetCount))
            return;
        IsSendingAsync = true;
        UserToken.SendAsync(packetOffset, packetCount);
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