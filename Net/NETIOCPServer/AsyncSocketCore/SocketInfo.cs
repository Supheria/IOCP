using System.Net.Sockets;

namespace Net;

public class SocketInfo
{
    public string? RemoteEndPoint { get; private set; } = null;

    public string? LocalEndPoint { get; private set; } = null;

    public DateTime ConnectTime { get; private set; } = DateTime.Now;

    public DateTime ActiveTime { get; private set; } = DateTime.Now;

    public DateTime DisconnectTime { get; private set; } = DateTime.Now;

    public void Connect(Socket socket)
    {
        RemoteEndPoint = socket.RemoteEndPoint?.ToString();
        LocalEndPoint = socket.LocalEndPoint?.ToString();
        ConnectTime = DateTime.Now;
        ActiveTime = DateTime.Now;
        DisconnectTime = DateTime.Now;
    }

    public void Active()
    {
        ActiveTime = DateTime.Now;
    }

    public void Disconnect()
    {
        DisconnectTime = DateTime.Now;
    }
}
