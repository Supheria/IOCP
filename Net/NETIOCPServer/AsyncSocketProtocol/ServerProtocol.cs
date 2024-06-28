using System.Net.Sockets;

namespace Net;

public partial class ServerProtocol(IocpServer server)
{
    IocpServer Server { get; } = server;

    object AcceptLocker { get; } = new();

    public IocpEventHandler? OnFileReceived;

    public IocpEventHandler? OnFileSent;

    public bool ProcessAccept(Socket? acceptSocket)
    {
        lock (AcceptLocker)
        {
            if (acceptSocket is null || Socket is not null)
                return false;
            Socket = acceptSocket;
            // 设置TCP Keep-alive数据包的发送间隔为10秒
            Socket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 1000 * 10, 1000 * 10), null);
            SocketInfo.Connect(acceptSocket);
            return true;
        }
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
}
