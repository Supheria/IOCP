using Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClientTest;

public class ClientOperator
{
    public ClientOperator(string name)
    {
        Name = name;
    }

    string Name { get; }

    public delegate void UpdateMessage(string message);

    public event UpdateMessage? OnUpdateMessage;

    IocpClientProtocol ClientFullHandlerSocket_MSG { get; set; }

    IocpClientProtocol ClientFullHandlerSocket_UPLOAD { get; set; }

    IocpClientProtocol ClientFullHandlerSoclet_DOWNLOAD { get; set; }

    private void OnConncet(IocpClientProtocol protocol)
    {
        OnUpdateMessage?.Invoke($"{protocol.UserInfo.Name} connect to {protocol.SocketInfo.RemoteEndPoint}");
    }

    void UploadEvent_UploadProcess()
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件上传完成");
    }

    void DownLoadEvent_DownLoadProcess()
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件下载完成");
    }

    public void Connect(string ipAddress, int port)
    {
        ClientFullHandlerSocket_MSG = new(); // 消息发送不需要挂接事件
        //ClientFullHandlerSocket_MSG.SetNoDelay(true);
        try
        {
            ClientFullHandlerSocket_MSG.Connect(ipAddress, port);//增强实时性，使用无延迟发送
            ClientFullHandlerSocket_MSG.OnConnect += OnConncet;
            ClientFullHandlerSocket_MSG.LocalFilePath = @"d:\temp";
            ClientFullHandlerSocket_MSG.OnReceiveMessage += appHandler_OnReceivedMsg;//接收到消息后处理事件
            ClientFullHandlerSocket_MSG.ReceiveAsync();
        }
        catch (Exception ex)
        {
            OnUpdateMessage?.Invoke($"{Name}: {ex.Message}");
            //ClientFullHandlerSocket_MSG.logger.Info("Connect failed");
            return;
        }
        //login
        if (ClientFullHandlerSocket_MSG.Login("admin", "password"))
        {
            new Task(() => OnUpdateMessage?.Invoke($"{Name}: login")).Start();
            //button_connect.Text = "Connected";
            //button_connect.Enabled = false;
            //ClientFullHandlerSocket_MSG.logger.Info("Login success");
        }
        else
        {
            //MessageBox.Show("Login failed");
            //ClientFullHandlerSocket_MSG.logger.Info("Login failed");
        }
    }

    public void Disconnet()
    {
        try
        {
            ClientFullHandlerSocket_MSG?.Close();
        }
        catch { }
        try
        {
            ClientFullHandlerSocket_UPLOAD?.Close();
        }
        catch { }
        try
        {
            ClientFullHandlerSoclet_DOWNLOAD?.Close();
        }
        catch { }
    }

    public void SendMessage(string message)
    {
        try
        {
            ClientFullHandlerSocket_MSG.SendMessage(message);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private void appHandler_OnReceivedMsg(string message)
    {
        //在通信框架外写业务逻辑
        if (message.Contains("result"))
        {
            OnUpdateMessage?.Invoke($"{Name}: {message}");
        }

    }

    public void UploadFile(string localFilePath)
    {
        if (ClientFullHandlerSocket_UPLOAD == null)
        {
            ClientFullHandlerSocket_UPLOAD = new();
            ClientFullHandlerSocket_UPLOAD.OnUpload += UploadEvent_UploadProcess; // 只挂接上传事件
            ClientFullHandlerSocket_UPLOAD.Connect("127.0.0.1", 8000);
            ClientFullHandlerSocket_UPLOAD.LocalFilePath = @"d:\temp";
            ClientFullHandlerSocket_UPLOAD.ReceiveAsync();
            ClientFullHandlerSocket_UPLOAD.Login("admin", "password");
        }
        ClientFullHandlerSocket_UPLOAD.Upload(localFilePath, "", new FileInfo(localFilePath).Name);
    }

    public void DownloadFile(string remoteFilePath)
    {
        if (ClientFullHandlerSoclet_DOWNLOAD == null)
        {
            ClientFullHandlerSoclet_DOWNLOAD = new();
            ClientFullHandlerSoclet_DOWNLOAD.OnDownload += DownLoadEvent_DownLoadProcess; // 只挂接下载事件
            ClientFullHandlerSoclet_DOWNLOAD.Connect("127.0.0.1", 8000);
            ClientFullHandlerSoclet_DOWNLOAD.LocalFilePath = "download";
            ClientFullHandlerSoclet_DOWNLOAD.ReceiveAsync();
            ClientFullHandlerSoclet_DOWNLOAD.Login("admin", "password");
        }
        FileInfo fi = new FileInfo(remoteFilePath);
        ClientFullHandlerSoclet_DOWNLOAD.Download(fi.DirectoryName, fi.Name, fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf("\\", StringComparison.Ordinal)));

    }
}
