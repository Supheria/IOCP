using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Net;

public partial class ClientProtocol
{
    public IocpEventHandler? OnUploaded;

    public IocpEventHandler? OnDownloaded;

    public IocpEventHandler<float>? OnUploading;

    public IocpEventHandler<float>? OnDownloading;

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
}
