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

    ClientProtocol Client { get; set; } = new();

    //ClientProtocol ClientFullHandlerSocket_UPLOAD { get; set; }

    //ClientProtocol ClientFullHandlerSoclet_DOWNLOAD { get; set; }

    private void OnConncet(IocpProtocol protocol)
    {
        OnUpdateMessage?.Invoke($"{protocol.UserInfo.Name} connect to {protocol.SocketInfo.RemoteEndPoint}");
    }

    void UploadEvent_UploadProcess(IocpProtocol protocol)
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件上传完成");
    }

    void DownLoadEvent_DownLoadProcess(IocpProtocol protocol)
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件下载完成");
    }

    public void Connect(string ipAddress, int port)
    {
        //Client = new(); // 消息发送不需要挂接事件
        //ClientFullHandlerSocket_MSG.SetNoDelay(true);
        try
        {
            Client.Connect(ipAddress, port);//增强实时性，使用无延迟发送
            Client.OnConnect += OnConncet;
            Client.RootDirectoryPath = @"d:\temp";
            Client.OnMessage += appHandler_OnReceivedMsg;//接收到消息后处理事件
            Client.ReceiveAsync();
        }
        catch (Exception ex)
        {
            OnUpdateMessage?.Invoke($"{Name}: {ex.Message}");
            //ClientFullHandlerSocket_MSG.logger.Info("Connect failed");
            return;
        }
        Client.Login("admin", "password");
        ////login
        //if ()
        //{
        //    //new Task(() => OnUpdateMessage?.Invoke($"{Name}: login")).Start();
        //    //button_connect.Text = "Connected";
        //    //button_connect.Enabled = false;
        //    //ClientFullHandlerSocket_MSG.logger.Info("Login success");
        //}
        //else
        //{
        //    //MessageBox.Show("Login failed");
        //    //ClientFullHandlerSocket_MSG.logger.Info("Login failed");
        //}
    }

    public void Disconnet()
    {
        Client?.Close();
    }

    public void SendMessage(string message)
    {
        try
        {
            Client.SendMessage(message);
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
        Client.OnUploaded += (IocpProtocol protocol) => OnUpdateMessage?.Invoke($"{Name}: 文件上传完成");
        Client.Connect("127.0.0.1", 8000);
        Client.RootDirectoryPath = @"d:\temp";
        Client.ReceiveAsync();
        Client.Login("admin", "password");
        Client.Upload(localFilePath, "", new FileInfo(localFilePath).Name);
    }

    public void DownloadFile(string remoteFilePath)
    {
        //if (ClientFullHandlerSocket_MSG == null)
        {
            //Client = new();
            Client.OnDownloaded += DownLoadEvent_DownLoadProcess; // 只挂接下载事件
            Client.Connect("127.0.0.1", 8000);
            Client.RootDirectoryPath = "download";
            Client.ReceiveAsync();
            Client.Login("admin", "password");
        }
        FileInfo fi = new FileInfo(remoteFilePath);
        Client.Download(fi.DirectoryName, fi.Name, fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf("\\", StringComparison.Ordinal)));

    }
}
