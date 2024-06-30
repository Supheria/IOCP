using LocalUtilities.IocpNet.Common;
using LocalUtilities.IocpNet.Serve;
using LocalUtilities.SimpleScript.Serialization;
using LocalUtilities.TypeGeneral;
using System;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace WarringStates.UI;

public class ClientForm : ResizeableForm
{
    public override string LocalName => nameof(ClientForm);

    private object FormLocker { get; } = new();

    IocpClient Client { get; } = new();

    TextBox HostAddress { get; } = new()
    {
        Text = "127.0.0.1"
    };

    NumericUpDown HostPort { get; } = new()
    {
        Value = 60,
    };

    TextBox UserName { get; } = new()
    {
        Text = "admin"
    };

    TextBox Password { get; } = new()
    {
        Text = "password"
    };

    Button SwitchButton { get; } = new()
    {
        Text = "Connect",
    };

    RichTextBox MessageBox { get; } = new();

    TextBox SendBox { get; } = new();

    Button SendButton { get; } = new()
    {
        Text = "Send",
    };

    ComboBox DirName { get; } = new();

    TextBox FilePath { get; } = new();

    Button FilePathButton { get; } = new()
    {
        Text = "..."
    };

    Button UploadButton { get; } = new()
    {
        Text = "Upload"
    };

    Button DownloadButton { get; } = new()
    {
        Text = "Download"
    };

    public ClientForm()
    {
        Text = "client";
        Controls.AddRange([
            HostAddress,
            HostPort,
            UserName,
            Password,
            SwitchButton,
            MessageBox,
            SendBox,
            SendButton,
            DirName,
            FilePath,
            FilePathButton,
            UploadButton,
            DownloadButton,
            ]);
        OnLoadForm += ClientForm_OnLoadForm;
        OnSaveForm += ClientForm_OnSaveForm;
        FormClosing += (_, _) => Client.Close();
        OnDrawClient += ClientForm_OnDrawClient;
        SwitchButton.Click += SwitchButton_Click;
        SendButton.Click += (_, _) => Client.SendMessage(SendBox.Text);
        FilePathButton.Click += FilePathButton_Click;
        UploadButton.Click += (_, _) => Client.Upload(DirName.Text, FilePath.Text);
        DownloadButton.Click += (_, _) => Client.Download(DirName.Text, FilePath.Text);
        Client.OnLog += UpdateMessage;
        Client.OnConnected += Client_OnConnected;
        Client.OnDisconnected += Client_OnDisconnected;
        Client.OnProcessing += UpdateFormText;
    }

    private void Client_OnDisconnected()
    {
        BeginInvoke(new Action(() =>
        {
            SwitchButton.Text = "Connect";
            HostAddress.Enabled = true;
            HostPort.Enabled = true;
            UserName.Enabled = true;
            Password.Enabled = true;
            Update();
        }));
    }

    private void Client_OnConnected()
    {
        BeginInvoke(new Action(() =>
        {
            SwitchButton.Text = "Disconnect";
            HostAddress.Enabled = false;
            HostPort.Enabled = false;
            UserName.Enabled = false;
            Password.Enabled = false;
            Update();
        }));
    }

    private void ClientForm_OnSaveForm(SsSerializer serializer)
    {
        serializer.WriteTag(nameof(HostAddress), HostAddress.Text);
        serializer.WriteTag(nameof(HostPort), HostPort.Value.ToString());
        serializer.WriteTag(nameof(UserName), UserName.Text);
        serializer.WriteTag(nameof(Password), Password.Text);
        serializer.WriteTag(nameof(DirName), DirName.Text);
        serializer.WriteTag(nameof(FilePath), FilePath.Text);
    }

    private void ClientForm_OnLoadForm(SsDeserializer deserializer)
    {
        HostAddress.Text = deserializer.ReadTag(nameof(HostAddress));
        HostPort.Value = deserializer.ReadTag(nameof(HostPort), int.Parse);
        UserName.Text = deserializer.ReadTag(nameof(UserName));
        Password.Text = deserializer.ReadTag(nameof(Password));
        DirName.Text = deserializer.ReadTag(nameof(DirName));
        FilePath.Text = deserializer.ReadTag(nameof(FilePath));
    }

    private void FilePathButton_Click(object? sender, EventArgs e)
    {
        var file = new OpenFileDialog();
        if (file.ShowDialog() is DialogResult.Cancel)
            return;
        FilePath.Text = file.FileName;
    }

    private void SwitchButton_Click(object? sender, EventArgs e)
    {
        if (Client.IsConnect)
            Client.Disconnected();
        else
            Client.Connect(HostAddress.Text, (int)HostPort.Value, UserName.Text, Password.Text);
    }

    private void UpdateMessage(string message)
    {
        BeginInvoke(() =>
        {
            MessageBox.Text += $"{message}\n";
            Update();
        });
    }

    private void UpdateFormText(string text)
    {
        BeginInvoke(() =>
        {
            Text = $"client - {text}";
            Update();
        });
    }

    private void ClientForm_OnDrawClient()
    {
        var width = (ClientWidth - Padding * 7) / 5;
        var top = ClientTop + Padding;
        //
        HostAddress.Left = ClientLeft + Padding;
        HostAddress.Top = top;
        HostAddress.Width = width;
        //
        HostPort.Left = HostAddress.Right + Padding;
        HostPort.Top = top;
        HostPort.Width = width;
        //
        UserName.Left = HostPort.Right + Padding;
        UserName.Top = top;
        UserName.Width = width;
        //
        Password.Left = UserName.Right + Padding;
        Password.Top = top;
        Password.Width = width;
        //
        SwitchButton.Left = Password.Right + Padding;
        SwitchButton.Top = top;
        SwitchButton.Width = width;
        //
        top = Password.Bottom + Padding;
        //
        MessageBox.Left = ClientLeft + Padding;
        MessageBox.Top = top;
        MessageBox.Width = ClientWidth - Padding * 2;
        MessageBox.Height = ClientHeight - HostAddress.Height - SendBox.Height - FilePath.Height - Padding * 6;
        //
        width = (ClientWidth - Padding * 3) / 4;
        //
        SendBox.Left = ClientLeft + Padding;
        SendBox.Top = MessageBox.Bottom + Padding;
        SendBox.Width = width * 3;
        //
        SendButton.Left = SendBox.Right + Padding;
        SendButton.Top = MessageBox.Bottom + Padding;
        SendButton.Width = width;
        //
        width = (ClientWidth - Padding * 10) / 12;
        var width2x = width * 3;
        top = SendButton.Bottom + Padding;
        //
        DirName.Left = ClientLeft + Padding;
        DirName.Top = top;
        DirName.Width = width2x;
        //
        FilePath.Left = DirName.Right + Padding;
        FilePath.Top = top;
        FilePath.Width = width2x + Padding;
        //
        FilePathButton.Left = FilePath.Right + Padding;
        FilePathButton.Top = top;
        FilePathButton.Width = width;
        //
        UploadButton.Left = FilePathButton.Right + Padding;
        UploadButton.Top = top;
        UploadButton.Width = width2x;
        //
        DownloadButton.Left = UploadButton.Right + Padding;
        DownloadButton.Top = top;
        DownloadButton.Width = width2x;
    }
}
