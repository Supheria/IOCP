using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Net;

public partial class ClientProtocol
{
    public delegate void HandleMessage(string message);

    public event HandleMessage? OnReceiveMessage;

    public event HandleEvent? OnConnect;

    public event HandleEvent? OnUploaded;

    public event HandleEvent? OnDownloaded;

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
        Connect(new IPEndPoint(ipAddress, port));
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

    public bool Active()
    {
        try
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Active);
            SendCommand(commandComposer);
            return true;
        }
        catch (Exception ex)
        {
            //记录日志
            //ErrorString = ex.Message;
            //Logger.Error(ex.Message);
            return false;
        }
    }

    public void HandleReceiveMessage(string message)
    {
        new Task(() => OnReceiveMessage?.Invoke(message)).Start();
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
