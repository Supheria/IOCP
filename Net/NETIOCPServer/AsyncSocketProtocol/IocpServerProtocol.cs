using System;
using System.Text;
using Net;

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

    protected IocpServer Server { get; } = server;

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

    // TODO: modify all below

    public virtual bool ProcessReceive(byte[] buffer, int offset, int count) //接收异步事件返回的数据，用于对数据进行缓存和分包
    {
        ActiveTime = DateTime.UtcNow;
        DynamicBufferManager receiveBuffer = UserToken.ReceiveBuffer;

        receiveBuffer.WriteBuffer(buffer, offset, count);
        bool result = true;
        while (receiveBuffer.DataCount > sizeof(int))
        {
            //按照长度分包
            int packetLength = BitConverter.ToInt32(receiveBuffer.Buffer, 0); //获取包长度
            if (UseNetByteOrder)
                packetLength = System.Net.IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序


            if ((packetLength > 10 * 1024 * 1024) | (receiveBuffer.DataCount > 10 * 1024 * 1024)) //最大Buffer异常保护
                return false;

            if ((receiveBuffer.DataCount) >= packetLength) //收到的数据达到包长度
            {
                result = ProcessPacket(receiveBuffer.Buffer, sizeof(int), packetLength);
                if (result)
                    receiveBuffer.Clear(packetLength); //从缓存中清理
                else
                    return result;
            }//未收完，继续接收
            else
            {
                return true;
            }
        }
        return true;
    }

    public virtual bool ProcessPacket(byte[] buffer, int offset, int count) //处理分完包后的数据，把命令和数据分开，并对命令进行解析
    {
        if (count < sizeof(int))
            return false;
        int commandLen = BitConverter.ToInt32(buffer, offset); //取出命令长度
        string tmpStr = Encoding.UTF8.GetString(buffer, offset + sizeof(int), commandLen);
        if (!CommandParser.DecodeProtocolText(tmpStr)) //解析命令
            return false;

        return ProcessCommand(buffer, offset + sizeof(int) + commandLen, count - sizeof(int) - sizeof(int) - commandLen); //处理命令,offset + sizeof(int) + commandLen后面的为数据，数据的长度为count - sizeof(int) - sizeof(int) - commandLen，注意是包的总长度－包长度所占的字节（sizeof(int)）－ 命令长度所占的字节（sizeof(int)） - 命令的长度
    }

    public virtual bool ProcessCommand(byte[] buffer, int offset, int count) //处理具体命令，子类从这个方法继承，buffer是收到的数据
    {
        return true;
    }

    public virtual void ProcessSend()
    {
        ActiveTime = DateTime.UtcNow;
        IsSendingAsync = false;
        AsyncSendBufferManager asyncSendBufferManager = UserToken.SendBuffer;
        asyncSendBufferManager.ClearFirstPacket(); //清除已发送的包
        int offset = 0;
        int count = 0;
        if (asyncSendBufferManager.GetFirstPacket(ref offset, ref count))
        {
            IsSendingAsync = true;
            UserToken.SendAsync(asyncSendBufferManager.DynamicBufferManager.Buffer, offset, count);
        }
        else
            SendCallback();
    }

    //发送回调函数，用于连续下发数据
    public virtual bool SendCallback()
    {
        return true;
    }

    public bool DoSendResult()
    {
        string commandText = CommandComposer.GetProtocolText();
        byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
        int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length; //获取总大小
        AsyncSendBufferManager asyncSendBufferManager = UserToken.SendBuffer;
        asyncSendBufferManager.StartPacket();
        asyncSendBufferManager.DynamicBufferManager.WriteInt(totalLength, false); //写入总大小
        asyncSendBufferManager.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); //写入命令大小
        asyncSendBufferManager.DynamicBufferManager.WriteBuffer(bufferUTF8); //写入命令内容
        asyncSendBufferManager.EndPacket();

        bool result = true;
        if (!IsSendingAsync)
        {
            int packetOffset = 0;
            int packetCount = 0;
            if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
            {
                IsSendingAsync = true;
                UserToken.SendAsync(asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
            }
        }
        return result;
    }

    public bool DoSendResult(byte[] buffer, int offset, int count)
    {
        string commandText = CommandComposer.GetProtocolText();//获取命令
        byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);//获取命令的字节数组
        int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length + count; //获取总大小(4个字节的包总长度+4个字节的命令长度+命令字节数组的长度+数据的字节数组长度)
        AsyncSendBufferManager asyncSendBufferManager = UserToken.SendBuffer;
        asyncSendBufferManager.StartPacket();
        asyncSendBufferManager.DynamicBufferManager.WriteInt(totalLength, false); //写入总大小
        asyncSendBufferManager.DynamicBufferManager.WriteInt(bufferUTF8.Length, false); //写入命令大小
        asyncSendBufferManager.DynamicBufferManager.WriteBuffer(bufferUTF8); //写入命令内容
        asyncSendBufferManager.DynamicBufferManager.WriteBuffer(buffer, offset, count); //写入二进制数据
        asyncSendBufferManager.EndPacket();
        bool result = true;
        if (!IsSendingAsync)
        {
            int packetOffset = 0;
            int packetCount = 0;
            if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
            {
                IsSendingAsync = true;
                UserToken.SendAsync(asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
            }
        }
        return result;
    }

    public bool DoSendBuffer(byte[] buffer, int offset, int count) //不是按包格式下发一个内存块，用于日志这类下发协议
    {
        AsyncSendBufferManager asyncSendBufferManager = UserToken.SendBuffer;
        asyncSendBufferManager.StartPacket();
        asyncSendBufferManager.DynamicBufferManager.WriteBuffer(buffer, offset, count);
        asyncSendBufferManager.EndPacket();

        bool result = true;
        if (!IsSendingAsync)
        {
            int packetOffset = 0;
            int packetCount = 0;
            if (asyncSendBufferManager.GetFirstPacket(ref packetOffset, ref packetCount))
            {
                IsSendingAsync = true;
                UserToken.SendAsync(asyncSendBufferManager.DynamicBufferManager.Buffer, packetOffset, packetCount);
            }
        }
        return result;
    }
}