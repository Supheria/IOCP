using Net;
using System.Collections;


namespace ClientDemo
{
    public partial class Client : Form
    {
        private IocpClientProtocol ClientFullHandlerSocket_MSG;
        private IocpClientProtocol ClientFullHandlerSocket_UPLOAD;
        private IocpClientProtocol ClientFullHandlerSoclet_DOWNLOAD;
        private bool stop = false;

        public Hashtable elementHashtable = new Hashtable();
        private int number = 0;

        public Client()
        {
            InitializeComponent();
        }



        void uploadEvent_uploadProcess()
        {
            MessageBox.Show("上传完成！");
        }

        void downLoadEvent_downLoadProcess()
        {
            MessageBox.Show("下载完成！");
        }

        private void button_connect_Click(object sender, EventArgs e)
        {
            ClientFullHandlerSocket_MSG = new();//消息发送不需要挂接事件
            //ClientFullHandlerSocket_MSG.SetNoDelay(true);
            try
            {
                ClientFullHandlerSocket_MSG.Connect(textBox_IP.Text, Convert.ToInt32(textBox_Port.Text));//增强实时性，使用无延迟发送
                ClientFullHandlerSocket_MSG.LocalFilePath = @"d:\temp";
                ClientFullHandlerSocket_MSG.Client.OnReceiveMessage += appHandler_OnReceivedMsg;//接收到消息后处理事件
                ClientFullHandlerSocket_MSG.ReceiveMessageHead();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                //ClientFullHandlerSocket_MSG.logger.Info("Connect failed");
                return;
            }
            //login
            if (ClientFullHandlerSocket_MSG.Login("admin", "password"))
            {
                button_connect.Text = "Connected";
                button_connect.Enabled = false;
                //ClientFullHandlerSocket_MSG.logger.Info("Login success");
            }
            else
            {
                MessageBox.Show("Login failed");
                //ClientFullHandlerSocket_MSG.logger.Info("Login failed");
            }
        }

        private void button_sendMsg_Click(object sender, EventArgs e)
        {
            try
            {
                ClientFullHandlerSocket_MSG.SendMessage(textBox_msg.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        void appHandler_OnReceivedMsg(string msg)
        {
            //在通信框架外写业务逻辑
            if (msg.Contains("result"))
            {
#if DEBUG                
                Console.WriteLine(msg);
#endif
            }

        }

        private void button_sendFile_Click(object sender, EventArgs e)
        {
            UpLoad(textBox_filePath.Text);
            //ClientFullHandlerSocket_MSG.DoUpload(textBox_filePath.Text, "", new FileInfo(textBox_filePath.Text).Name);
        }
        private void UpLoad(string fileFullPath)//使用单独Socket来上传文件
        {
            if (ClientFullHandlerSocket_UPLOAD == null)
            {
                ClientFullHandlerSocket_UPLOAD = new();
                ClientFullHandlerSocket_UPLOAD.Client.OnUpload += uploadEvent_uploadProcess; // 只挂接上传事件
                ClientFullHandlerSocket_UPLOAD.Connect("127.0.0.1", 8000);
                ClientFullHandlerSocket_UPLOAD.LocalFilePath = @"d:\temp";
                ClientFullHandlerSocket_UPLOAD.ReceiveMessageHead();
                ClientFullHandlerSocket_UPLOAD.Login("admin", "password");
            }
            ClientFullHandlerSocket_UPLOAD.DoUpload(fileFullPath, "", new FileInfo(fileFullPath).Name);
        }





        private void button_fileSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "选择要发送的文件";
            ofd.Filter = "所有文件(*.*)|*.*";
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBox_filePath.Text = ofd.FileName;
            }
        }

        private void button_download_Click(object sender, EventArgs e)
        {
            DownLoad(textBox_remoteFilePath.Text);

        }
        private void DownLoad(string remoteFileFullPath)//使用单独Socket来下载文件
        {
            if (ClientFullHandlerSoclet_DOWNLOAD == null)
            {
                ClientFullHandlerSoclet_DOWNLOAD = new();
                ClientFullHandlerSoclet_DOWNLOAD.Client.OnDownload += downLoadEvent_downLoadProcess; // 只挂接下载事件
                ClientFullHandlerSoclet_DOWNLOAD.Connect("127.0.0.1", 8000);
                ClientFullHandlerSoclet_DOWNLOAD.LocalFilePath = @"d:\temp";
                ClientFullHandlerSoclet_DOWNLOAD.ReceiveMessageHead();
                ClientFullHandlerSoclet_DOWNLOAD.Login("admin", "password");
            }
            FileInfo fi = new FileInfo(remoteFileFullPath);
            ClientFullHandlerSoclet_DOWNLOAD.DoDownload(fi.DirectoryName, fi.Name, fi.DirectoryName.Substring(fi.DirectoryName.LastIndexOf("\\", StringComparison.Ordinal)));
        }




    }

}
