using LocalUtilities.TypeGeneral;

namespace ClientTest;

public class ClientTestBoostForm : ResizeableForm
{
    public override string LocalName => nameof(ClientTestBoostForm);

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
            ]);
        OnDrawingClient += DrawClient;
        SwitchButton.Click += Start_Click;
        Timer.Interval = 300;
        Timer.Elapsed += (_, _) => Test();
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
        c1.Connet(ipAddress, port);
        //
        //Thread.Sleep(1000);
        //
        c2 = new("c2");
        c2.OnUpdateMessage += UpdateMessage;
        c2.Connet(ipAddress, port);
        //
        //Thread.Sleep(1000);
        //
        c2.Disconnet();
        c1.Disconnet();
        c3 = new("c3");
        c3.OnUpdateMessage += UpdateMessage;
        c3.Connet(ipAddress, port);
        //
        Thread.Sleep(10);
        //
        c2.Connet(ipAddress, port);
        c1.Connet(ipAddress, port);
        c1.SendMessage("c1;Hello World;");
        c1.UploadFile(UploadFilePath);
        //
        Thread.Sleep(10);
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

    static string UploadFilePath => "express test.bmp";

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
        var width = ClientWidth / 7;
        var top = ClientTop + Padding;
        // IpAddress
        IpAddress.Left = ClientLeft + width;
        IpAddress.Top = top;
        IpAddress.Width = width;
        // Port
        Port.Left = IpAddress.Right + width;
        Port.Top = top;
        Port.Width = width;
        // SwitchButton
        SwitchButton.Left = Port.Right + width;
        SwitchButton.Top = top;
        SwitchButton.Width = width;
        // MessageBox
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = SwitchButton.Bottom + Padding;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - SwitchButton.Height - Padding * 3;
    }
}
