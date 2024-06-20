using Net;
using System;
using System.Windows.Forms;

namespace Net.ServerDemo
{
    public partial class Server : Form
    {
        public Server()
        {
            InitializeComponent();
        }

        private void button_start_Click(object sender, EventArgs e)
        {
            if (button_start.Text == "Start")
            {
                ServerInstance.Init("upload", 8000, 100, 1 * 60 * 1000, "0.0.0.0");//使用0.0.0.0作为绑定IP，则本机所有的IPv4地址都将绑定
                ServerInstance.appHandler = new AppHandler();
                ServerInstance.appHandler.OnReceivedMsg += new AppHandler.HandlerReceivedMsg(appHandler_OnReceivedMsg);//接收到消息后的处理事件
                ServerInstance.Logger.Info("Server Started");
                button_start.Text = "Stop";
            }
            else
            {
                ServerInstance.Close();
                ServerInstance.Logger.Info("Server Stoped");
                button_start.Text = "Start";
            }
        }

        void appHandler_OnReceivedMsg(string msg, ServerFullHandlerProtocol fullHandlerSocketProtocol)
        {
            if (msg.Contains(";"))
            {
                string[] receivedMsg = msg.Split(';');
                foreach (var s in receivedMsg)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        MessageBox.Show(s);
                    }
                }
            }
            else if (msg.Contains("computer"))
            {
                fullHandlerSocketProtocol.SendMessage("result0123456789.9876543210");
            }
        }

        private void Server_FormClosing(object sender, FormClosingEventArgs e)
        {
            ServerInstance.Close();
        }
    }
}
