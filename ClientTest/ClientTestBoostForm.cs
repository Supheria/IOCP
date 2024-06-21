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

    Button Start { get; } = new()
    {
        Text = "start test"
    };

    RichTextBox MessageBox { get; } = new();

    protected override void InitializeComponent()
    {
        Controls.AddRange([
            Start,
            IpAddress,
            Port,
            MessageBox,
            ]);
        OnDrawingClient += DrawClient;
        Start.Click += Start_Click;
    }

    private void Start_Click(object? sender, EventArgs e)
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
        c3 = new("c3");
        c3.OnUpdateMessage += UpdateMessage;
        c3.Connet(ipAddress, port);
        //
        Thread.Sleep(10);
        //
        //c1.SendMessage("c1;Hello World;");
        c1.UploadFile(UploadFilePath);
        //
        Thread.Sleep(10);
        //
        var uploadedPath = Path.Combine("upload", UploadFilePath);
        var downloadedPath = Path.Combine("download", uploadedPath);
        if (File.Exists(downloadedPath))
            File.Delete(downloadedPath);
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
        // Start
        Start.Left = Port.Right + width;
        Start.Top = top;
        Start.Width = width;
        // MessageBox
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = Start.Bottom + Padding;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - Start.Height - Padding * 3;
    }
}
