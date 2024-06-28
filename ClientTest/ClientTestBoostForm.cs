using LocalUtilities.FileHelper;
using LocalUtilities.IocpNet.Protocol;
using LocalUtilities.TypeGeneral;

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

        Client.OnUploaded += (p) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: upload file success");
        Client.OnDownloaded += (p) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: download file success");
        Client.OnUploading += (p, progress) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: uploading {progress}%");
        Client.OnDownloading += (p, progress) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: downloading {progress}%");
        Client.OnMessage += (p, m) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: {m}");
        Client.OnException += (p, ex) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: {ex.Message}");
        Client.OnClosed += (p) => UpdateMessage($"{p.SocketInfo.LocalEndPoint}: closed");
        //Client.Connect(ipAddress, port);
    }

    static string TestFilePath => "test";

    private void UploadButton_Click(object? sender, EventArgs e)
    {

        var ipAddress = IpAddress.Text;
        _ = int.TryParse(Port.Text, out var port);
        Client.Connect(ipAddress, port);
        Client.Login("admin", "password");
        Client.Upload(Client.UserInfo?.Name ?? "default", TestFilePath, true);
    }

    private void DownloadButton_Click(object? sender, EventArgs e)
    {

        var ipAddress = IpAddress.Text;
        _ = int.TryParse(Port.Text, out var port);
        Client.Connect(ipAddress, port);
        Client.Login("admin", "password");
        Client.Download(Client.UserInfo?.Name ?? "default", TestFilePath, true);
    }

    private string GetUploadPath(string localPath)
    {
        return Path.Combine(RootDirectory, "upload", localPath);
    }

    private string GetDownloadPath(string localPath)
    {
        return Path.Combine(RootDirectory, "download", localPath);
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
        c1.UploadFile(TestFilePath);
        //
        Thread.Sleep(100);
        //
        c2.SendMessage("c2;Hello Host;");
        var uploadedPath = Path.Combine("upload", TestFilePath);
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

    string RootDirectory { get; } = Directory.CreateDirectory(nameof(ClientTestBoostForm)).FullName;

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
        MessageBox.Height = ClientHeight - SwitchButton.Height * 2 - Padding * 4;
    }
}
