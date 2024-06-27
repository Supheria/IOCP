using LocalUtilities.TypeGeneral;
using Net;
using static ClientTest.ClientOperator;

namespace ClientTest;

public class ClientTestBoostForm : ResizeableForm
{
    public override string LocalName => nameof(ClientTestBoostForm);

    ClientProtocol Client { get; } = new();

    TextBox IpAddress { get; } = new()
    {
        Text = "127.0.0.1",
    };

    TextBox Port { get; } = new()
    {
        Text = 8000.ToString(),
    };

    Button SwitchButton { get; } = new()
    {
        Text = "start"
    };

    Button SingleButton { get; } = new()
    {
        Text = "single"
    };

    Button UploadButton { get; } = new()
    {
        Text = "upload"
    };

    Button DownloadButton { get; } = new()
    {
        Text = "download"
    };

    RichTextBox MessageBox { get; } = new();

    System.Timers.Timer Timer { get; } = new();

    bool IsStart { get; set; } = false;

    protected override void InitializeComponent()
    {
        Controls.AddRange([
            SwitchButton,
            IpAddress,
            Port,
            MessageBox,
            SingleButton,
            UploadButton,
            DownloadButton,
            ]);
        OnDrawingClient += DrawClient;
        SwitchButton.Click += Start_Click;
        SingleButton.Click += SingleButton_Click;
        UploadButton.Click += UploadButton_Click;
        DownloadButton.Click += DownloadButton_Click;
        Timer.Interval = 100;
        Timer.Elapsed += (_, _) => Test();


        var ipAddress = IpAddress.Text;
        _ = int.TryParse(Port.Text, out var port);
        Client.Connect(ipAddress, port);
        Client.OnDownload += (IocpProtocol protocol) => UpdateMessage($"文件下载完成");
        Client.OnUpload += (IocpProtocol protocol) => UpdateMessage($"文件上传完成");
    }

    private void DownloadButton_Click(object? sender, EventArgs e)
    {
        //Client.Connect(ipAddress, port);
        //Client.RootDirectoryPath = "download";
        Client.ReceiveAsync();
        Client.Login("admin", "password");
        Client.RootDirectoryPath = "download";
        var uploadedPath = Path.Combine("upload", UploadFilePath);
        var downloadedPath = Path.Combine("download", uploadedPath);
        if (File.Exists(downloadedPath))
        {
            try
            {
                File.Delete(downloadedPath);
            }
            catch { }
        }
        FileInfo fi = new FileInfo(uploadedPath);
        Client.Download(fi.DirectoryName, fi.Name, fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf("\\", StringComparison.Ordinal)));
    }

    private void UploadButton_Click(object? sender, EventArgs e)
    {
        //Client.Connect(ipAddress, port);
        //Client.RootDirectoryPath = @"d:\temp";
        Client.ReceiveAsync();
        Client.Login("admin", "password");
        Client.Upload(UploadFilePath, "", new FileInfo(UploadFilePath).Name);
    }

    private void SingleButton_Click(object? sender, EventArgs e)
    {
        Test();
    }

    private void Start_Click(object? sender, EventArgs e)
    {
        if (!IsStart)
        {
            Timer.Start();
            SwitchButton.Text = "Stop";
        }
        else
        {
            Timer.Stop();
            SwitchButton.Text = "Start";
        }
        IsStart = !IsStart;
    }

    private void Test()
    {
        var ipAddress = IpAddress.Text;
        _ = int.TryParse(Port.Text, out var port);
        ClientOperator c1, c2, c3;
        //
        c1 = new("c1");
        c1.OnUpdateMessage += UpdateMessage;
        c1.Connect(ipAddress, port);
        //
        //Thread.Sleep(1000);
        //
        c2 = new("c2");
        c2.OnUpdateMessage += UpdateMessage;
        c2.Connect(ipAddress, port);
        //
        //Thread.Sleep(1000);
        //
        c2.Disconnet();
        c1.Disconnet();
        c3 = new("c3");
        c3.OnUpdateMessage += UpdateMessage;
        //c3.Connect(ipAddress, port);
        //
        Thread.Sleep(10);
        //
        c2.Connect(ipAddress, port);
        c1.Connect(ipAddress, port);
        c1.SendMessage("c1;Hello World;");
        c1.UploadFile(UploadFilePath);
        //
        Thread.Sleep(100);
        //
        c2.SendMessage("c2;Hello Host;");
        var uploadedPath = Path.Combine("upload", UploadFilePath);
        var downloadedPath = Path.Combine("download", uploadedPath);
        if (File.Exists(downloadedPath))
        {
            try
            {
                File.Delete(downloadedPath);
            }
            catch { }
        }
        c3.DownloadFile(uploadedPath);
        //
        //Thread.Sleep(10000);
        //c3.SendMessage("file down");
        //c3.Disconnet();
        //c1.Disconnet();
    }

    static string UploadFilePath => "express test";

    private void UpdateMessage(string message)
    {
        lock (MessageBox)
        {
            Invoke(new Action(() =>
            {
                MessageBox.Text += $"{message}\n";
                Update();
            }));
        }
    }

    private void DrawClient()
    {
        var width = ClientWidth / 5;
        var top = ClientTop + Padding;
        //
        IpAddress.Left = ClientLeft + width;
        IpAddress.Top = top;
        IpAddress.Width = width;
        //
        Port.Left = IpAddress.Right + width;
        Port.Top = top;
        Port.Width = width;
        //
        width = ClientWidth / 9;
        top = Port.Bottom + Padding;
        SingleButton.Left = ClientLeft + width;
        SingleButton.Top = top;
        SingleButton.Width = width;
        //
        SwitchButton.Left = SingleButton.Right + width;
        SwitchButton.Top = top;
        SwitchButton.Width = width;
        //
        UploadButton.Left = SwitchButton.Right + width;
        UploadButton.Top = top;
        UploadButton.Width = width;
        //
        DownloadButton.Left = UploadButton.Right + width;
        DownloadButton.Top = top;
        DownloadButton.Width = width;
        //
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = DownloadButton.Bottom + Padding;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - SwitchButton.Height * 2 - Padding * 3;
    }
}
