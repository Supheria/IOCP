
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
        Text = "Start"
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

    private void Server_OnReceiveMessage(string message, ServerFullHandlerProtocol protocol)
    {
        if (message.Contains(";"))
        {
            var sentence = message.Split(';');
            foreach (var s in sentence)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    UpdateMessage($"{protocol.UserToken.SocketInfo.RemoteEndPoint}{s}");
                }
            }
        }
        else if (message.Contains("computer"))
        {
            protocol.SendMessage("result0123456789.9876543210");
        }
    }

    private void Server_OnClientNumberChange(IocpServer.ClientState state, AsyncUserToken userToken)
    {
        if (state is IocpServer.ClientState.Connect)
        {
            UpdateMessage($"{userToken.SocketInfo.RemoteEndPoint} connect");
        }
        else
        {
            UpdateMessage($"{userToken.SocketInfo.RemoteEndPoint} disconnect");
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
