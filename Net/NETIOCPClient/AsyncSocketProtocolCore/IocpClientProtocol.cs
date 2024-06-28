using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Net;

public delegate void HandleMessage(string message);

public partial class ClientProtocol
{
    public event HandleMessage? OnMessage;

    public event HandleEvent? OnConnect;

    public event HandleEvent? OnUploaded;

    public event HandleEvent? OnDownloaded;

    EndPoint? RemoteEndPoint { get; set; } = null;

    object ConnectLocker { get; } = new();

    public void Connect(string host, int port)
    {
        IPAddress ipAddress;
        if (Regex.Matches(host, "[a-zA-Z]").Count > 0)//支持域名解析
        {
            var ipHostInfo = Dns.GetHostEntry(host);
            ipAddress = ipHostInfo.AddressList[0];
        }
        else
        {
            ipAddress = IPAddress.Parse(host);
        }
        RemoteEndPoint = new IPEndPoint(ipAddress, port);
        Connect(RemoteEndPoint);
    }

    private void Connect()
    {
        Connect(RemoteEndPoint);
    }

    private void Connect(EndPoint? remoteEndPoint)
    {
        lock (ConnectLocker)
        {
            try
            {
                if (Socket is not null)
                    return;
                var connectArgs = new SocketAsyncEventArgs()
                {
                    RemoteEndPoint = remoteEndPoint
                };
                connectArgs.Completed += (_, args) => ProcessConnect(args);
                Socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (!Socket.ConnectAsync(connectArgs))
                    ProcessConnect(connectArgs);
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
        }
    }

    private void ProcessConnect(SocketAsyncEventArgs connectArgs)
    {
        if (connectArgs.ConnectSocket is null)
        {
            Socket?.Close();
            Socket?.Dispose();
            return;
        }
        ReceiveAsync();
        //ConnectDone.Set();
        new Task(() => OnConnect?.Invoke(this)).Start();
        SocketInfo.Connect(connectArgs.ConnectSocket);
    }

    public bool CheckErrorCode(CommandParser commandParser)
    {
        commandParser.GetValueAsInt(ProtocolKey.Code, out var errorCode);
        if ((ProtocolCode)errorCode is ProtocolCode.Success)
            return true;
        else
        {
            //ErrorString = ProtocolCode.GetErrorCodeString(errorCode);
            return false;
        }
    }

    public void HandleMessage(string message)
    {
        new Task(() => OnMessage?.Invoke(message)).Start();
    }

    public void HandleDownload()
    {
        new Task(() => OnDownloaded?.Invoke(this)).Start();
    }

    public void HandleUploaded()
    {
        new Task(() => OnUploaded?.Invoke(this)).Start();
    }
}
