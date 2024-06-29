using LocalUtilities.IocpNet.Protocol;
using LocalUtilities.IocpNet.Serve;
using LocalUtilities.TypeGeneral;
using System.Windows.Forms;

namespace WarringStates.UI;

internal class HostForm : ResizeableForm
{
    public override string LocalName => nameof(HostForm);

    IocpHost Host { get; set; } = new(1000);

    NumericUpDown Port { get; } = new()
    {
        Value = 60,
    };

    Button SwitchButton { get; } = new()
    {
        Text = "Start"
    };

    Label ParallelCount { get; } = new()
    {
        TextAlign = ContentAlignment.MiddleCenter,
        Text = "0",
    };

    RichTextBox MessageBox { get; } = new();

    TextBox SendBox { get; } = new();

    Button SendButton { get; } = new()
    {
        Text = "Send",
    };

    public HostForm()
    {
        Text = "tcp server";
        Controls.AddRange([
            Port,
            SwitchButton,
            ParallelCount,
            MessageBox,
            SendBox,
            SendButton,
            ]);
        OnDrawClient += DrawClient;
        SwitchButton.Click += SwitchButton_Click;
        Host.OnLog += UpdateMessage;
        Host.OnParallelRemainChange += Host_OnParallelRemainChange;
    }

    private void Host_OnParallelRemainChange(int args)
    {
        lock (ParallelCount)
        {
            Invoke(() =>
            {
                ParallelCount.Text = args.ToString();
                Update();
            });
        }
    }

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

    private void SwitchButton_Click(object? sender, EventArgs e)
    {
        if (Host.IsStart)
        {
            Host.Stop();
            if (!Host.IsStart)
                SwitchButton.Text = "Start";
            else
                System.Windows.Forms.MessageBox.Show($"close server failed");
        }
        else
        {
            Host.Start((int)Port.Value);
            if (Host.IsStart)
                SwitchButton.Text = "Close";
            else
                System.Windows.Forms.MessageBox.Show($"start server failed");
        }
    }

    private void DrawClient()
    {
        var width = (ClientWidth - Padding * 5) / 3;
        var top = ClientTop + Padding;
        //
        Port.Left = ClientLeft + Padding;
        Port.Top = top;
        Port.Width = width;
        //
        SwitchButton.Left = Port.Right + Padding;
        SwitchButton.Top = top;
        SwitchButton.Width = width;
        //
        ParallelCount.Left = SwitchButton.Right + Padding;
        ParallelCount.Top = top;
        ParallelCount.Width = width;
        //
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = SwitchButton.Bottom + Padding;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - Port.Height - SendButton.Height - Padding * 4;
        //
        top = MessageBox.Bottom + Padding;
        //
        SendBox.Left = ClientLeft + Padding;
        SendBox.Top = top;
        SendBox.Width = width * 2 + Padding;
        //
        SendButton.Left = SendBox.Right + Padding;
        SendButton.Top = top;
        SendButton.Width = width;
    }
}
