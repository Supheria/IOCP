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
        DownLoadEvent.downLoadProcess += new DownloadEvent.DownLoadProcess(DownLoadEvent_DownLoadProcess);
        UploadEvent.uploadProcess += new UploadEvent.UploadProcess(UploadEvent_UploadProcess);
    }

    string Name { get; }

    public delegate void UpdateMessage(string message);

    public event UpdateMessage? OnUpdateMessage;

    AsyncClientFullHandlerSocket ClientFullHandlerSocket_MSG { get; set; }

    AsyncClientFullHandlerSocket ClientFullHandlerSocket_UPLOAD { get; set; }

    AsyncClientFullHandlerSocket ClientFullHandlerSoclet_DOWNLOAD { get; set; }

    /// <summary>
    /// 下载完成事件
    /// </summary>
    DownloadEvent DownLoadEvent { get; } = new();

    /// <summary>
    /// 上传完成事件
    /// </summary>
    UploadEvent UploadEvent { get; } = new();

    void UploadEvent_UploadProcess()
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件上传完成");
    }

    void DownLoadEvent_DownLoadProcess()
    {
        OnUpdateMessage?.Invoke($"{Name}: 文件下载完成");
    }

    public void Connet(string ipAddress, int port)
    {
        ClientFullHandlerSocket_MSG = new(null, null);//消息发送不需要挂接事件
        //ClientFullHandlerSocket_MSG.SetNoDelay(true);
        try
        {
            ClientFullHandlerSocket_MSG.Connect(ipAddress, port);//增强实时性，使用无延迟发送
            ClientFullHandlerSocket_MSG.localFilePath = @"d:\temp";
            ClientFullHandlerSocket_MSG.appHandler = new AppHandler();
            ClientFullHandlerSocket_MSG.appHandler.OnReceivedMsg += new AppHandler.HandlerReceivedMsg(appHandler_OnReceivedMsg);//接收到消息后处理事件
            ClientFullHandlerSocket_MSG.ReceiveMessageHead();
        }
        catch (Exception ex)
        {
            OnUpdateMessage?.Invoke($"{Name}: {ex.Message}");
            //ClientFullHandlerSocket_MSG.logger.Info("Connect failed");
            return;
        }
        //login
        if (ClientFullHandlerSocket_MSG.DoLogin("admin", "password"))
        {
            OnUpdateMessage?.Invoke($"{Name}: connected");
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
            ClientFullHandlerSocket_MSG.Disconnect();
        }
        catch { }
        try
        {
            ClientFullHandlerSocket_UPLOAD.Disconnect();
        }
        catch { }
        try
        {
            ClientFullHandlerSoclet_DOWNLOAD.Disconnect();
        }
        catch { }
    }

    public void SendMessage(string message)
    {
        try
        {
            ClientFullHandlerSocket_MSG.SendMessageQuick(message);
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
            ClientFullHandlerSocket_UPLOAD = new AsyncClientFullHandlerSocket(null, UploadEvent);//只挂接上传事件
            ClientFullHandlerSocket_UPLOAD.Connect("127.0.0.1", 8000);
            ClientFullHandlerSocket_UPLOAD.localFilePath = @"d:\temp";
            ClientFullHandlerSocket_UPLOAD.ReceiveMessageHead();
            ClientFullHandlerSocket_UPLOAD.DoLogin("admin", "password");
        }
        ClientFullHandlerSocket_UPLOAD.DoUpload(localFilePath, "", new FileInfo(localFilePath).Name);
    }

    public void DownloadFile(string remoteFilePath)
    {
        if (ClientFullHandlerSoclet_DOWNLOAD == null)
        {
            ClientFullHandlerSoclet_DOWNLOAD = new AsyncClientFullHandlerSocket(DownLoadEvent, null);//只挂接下载事件
            ClientFullHandlerSoclet_DOWNLOAD.Connect("127.0.0.1", 8000);
            ClientFullHandlerSoclet_DOWNLOAD.localFilePath = "download";
            ClientFullHandlerSoclet_DOWNLOAD.ReceiveMessageHead();
            ClientFullHandlerSoclet_DOWNLOAD.DoLogin("admin", "password");
        }
        FileInfo fi = new FileInfo(remoteFilePath);
        ClientFullHandlerSoclet_DOWNLOAD.DoDownload(fi.DirectoryName, fi.Name, fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf("\\", StringComparison.Ordinal)));

    }
}
