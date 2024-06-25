

using LocalUtilities.TypeGeneral;
using Net;
using System.Text;
using System.Windows.Forms;

namespace WarringStates.UI;

internal class ServerForm : ResizeableForm
{
    public override string LocalName => nameof(ServerForm);

    IocpServer Server { get; } = new(1000, 1 * 60 * 1000);

    Button SwitchButton { get; } = new()
    {
        //Text = "Start"
        // TODO: debug using
        Text = "Stop"
    };

    NumericUpDown Port { get; } = new()
    {
        Maximum = short.MaxValue,
        Value = 8000,
    };

    RichTextBox MessageBox { get; } = new();

    Label ParallelRemain { get; } = new()
    {
        TextAlign = ContentAlignment.MiddleRight,
    };

    protected override void InitializeComponent()
    {
        Text = "tcp server";
        Controls.AddRange([
            SwitchButton,
            Port,
            MessageBox,
            ParallelRemain,
            ]);
        OnDrawingClient += DrawClient;
        SwitchButton.Click += SwitchButton_Click;
        Server.OnClientNumberChange += Server_OnClientNumberChange;
        Server.OnReceiveMessage += Server_OnReceiveMessage;
        //Server.OnReceiveClientData += Server_ReceiveClientData;
        Server.OnParallelRemainChange += Server_OnParalleRemainChange;
        Server.OnTip += Server_OnTip;
        Shown += (_, _) => Server.Start((int)Port.Value);
    }

    private void Server_OnTip(string tip, IocpServerProtocol protocol)
    {
        UpdateMessage($"tip: {protocol.SocketInfo.RemoteEndPoint} {tip}");
    }

    private void Server_OnParalleRemainChange(int remain)
    {
        lock (ParallelRemain)
        {
            Invoke(new Action(() =>
            {
                ParallelRemain.Text = $"remain: {remain}";
                Update();
            }));
        }
    }

    private void Server_OnReceiveMessage(string message, IocpServerProtocol protocol)
    {
        if (message.Contains(";"))
        {
            var sentence = message.Split(';');
            foreach (var s in sentence)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    UpdateMessage($"{protocol.SocketInfo.RemoteEndPoint}: {s}");
                }
            }
        }
        else if (message.Contains("computer"))
        {
            // TODO: use SendAsync directly
            protocol.SendMessage("result0123456789.9876543210");
            //protocol.SendAsync("result0123456789.9876543210");
        }
    }

    private void Server_OnClientNumberChange(IocpServer.ClientState state, IocpServerProtocol protocol)
    {
        if (state is IocpServer.ClientState.Connect)
        {
            UpdateMessage($"{protocol.SocketInfo.RemoteEndPoint} connect");
        }
        else
        {
            UpdateMessage($"{protocol.SocketInfo.RemoteEndPoint} disconnect");
        }
    }

    //private void Server_ReceiveClientData(AsyncClientProfile client, byte[] buff)
    //{
    //    UpdateMessage($"{client.RemoteEndPoint}: {Encoding.UTF8.GetString(buff)}");
    //}

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
        if (Server.IsStart)
        {
            Server.Stop();
            if (!Server.IsStart)
                SwitchButton.Text = "Start";
            else
                System.Windows.Forms.MessageBox.Show($"close server failed");
        }
        else
        {
            Server.Start((int)Port.Value);
            if (Server.IsStart)
                SwitchButton.Text = "Close";
            else
                System.Windows.Forms.MessageBox.Show($"start server failed");
        }
    }

    private void DrawClient()
    {
        var width = ClientWidth / 5;
        //
        Port.Left = ClientLeft + width;
        Port.Top = ClientTop + Padding;
        Port.Width = width;
        //
        SwitchButton.Left = Port.Right + width;
        SwitchButton.Top = ClientTop + Padding;
        SwitchButton.Width = width;
        //
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = SwitchButton.Bottom + Padding;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - Port.Height - ParallelRemain.Height - Padding * 2;
        //
        ParallelRemain.Left = ClientLeft + Padding;
        ParallelRemain.Top = MessageBox.Bottom;
        ParallelRemain.Width = ClientWidth - Padding * 2;
    }
}
