using System.Net.Sockets;
using System.Text;

namespace Net;

public abstract partial class IocpClientProtocol(IocpProtocolTypes type)
{
    public IocpClient Client { get; } = new();

    protected string Host { get; private set; } = "";

    protected int Port { get; private set; } = 0;

    protected IocpProtocolTypes Type { get; } = type;

    // HACK: protected int SocketTimeOutMS { get { return Client.SendTimeout; } set { Client.SendTimeout = value; Client.TimeoutMilliseconds = value; } }

    /// <summary>
    /// 长度是否使用网络字节顺序
    /// </summary>
    public bool NetByteOrder { get; set; } = false;

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
    protected DynamicBufferManager SendBuffer { get; } = new(ConstTabel.ReceiveBufferSize);

    /// <summary>
    /// 设置SOCKET是否延迟发送
    /// </summary>
    /// <param name="NoDelay"></param>
    public void SetNoDelay(bool NoDelay)
    {
        Client.Core.NoDelay = NoDelay;
    }

    public void Connect(string host, int port)
    {
        try
        {
            if (Client.Connect(host, port))
            {
                byte[] socketFlag = [(byte)Type];
                Client.Send(Client.Core, socketFlag, 0, 1, SocketFlags.None); //发送标识            
                Host = host;
                Port = port;
            }
            else
                throw new Exception("Connection failed");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // TODO: remove this shit and use Client.Disconnect directly
    public void Disconnect()
    {
        Client.Disconnect();
        //Client = new IocpClient();
    }

    public void SendCommand()
    {
        SendCommand([], 0, 0);
    }

    public void SendCommand(byte[] buffer, int offset, int count)
    {
        string commandText = CommandComposer.GetProtocolText();
        byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
        int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length + count; //获取总大小
        SendBuffer.Clear();
        SendBuffer.WriteInt(totalLength, false); //写入总大小
        SendBuffer.WriteInt(bufferUTF8.Length, false); //写入命令大小
        SendBuffer.WriteBuffer(bufferUTF8); //写入命令内容
        SendBuffer.WriteBuffer(buffer, offset, count); //写入二进制数据
        Client.Send(Client.Core, SendBuffer.Buffer, 0, SendBuffer.DataCount, SocketFlags.None);
    }
}
