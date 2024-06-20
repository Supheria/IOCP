namespace ClientDemo
{
    partial class Client
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.button_connect = new System.Windows.Forms.Button();
            this.textBox_msg = new System.Windows.Forms.TextBox();
            this.button_sendMsg = new System.Windows.Forms.Button();
            this.label_msg = new System.Windows.Forms.Label();
            this.label_file = new System.Windows.Forms.Label();
            this.button_sendFile = new System.Windows.Forms.Button();
            this.textBox_filePath = new System.Windows.Forms.TextBox();
            this.button_fileSelect = new System.Windows.Forms.Button();
            this.textBox_remoteFilePath = new System.Windows.Forms.TextBox();
            this.button_download = new System.Windows.Forms.Button();
            this.label_RPath = new System.Windows.Forms.Label();
            this.textBox_IP = new System.Windows.Forms.TextBox();
            this.label_IP = new System.Windows.Forms.Label();
            this.label_port = new System.Windows.Forms.Label();
            this.textBox_Port = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // button_connect
            // 
            this.button_connect.Location = new System.Drawing.Point(635, 74);
            this.button_connect.Name = "button_connect";
            this.button_connect.Size = new System.Drawing.Size(75, 23);
            this.button_connect.TabIndex = 0;
            this.button_connect.Text = "Connect";
            this.button_connect.UseVisualStyleBackColor = true;
            this.button_connect.Click += new System.EventHandler(this.button_connect_Click);
            // 
            // textBox_msg
            // 
            this.textBox_msg.Location = new System.Drawing.Point(137, 174);
            this.textBox_msg.Name = "textBox_msg";
            this.textBox_msg.Size = new System.Drawing.Size(423, 20);
            this.textBox_msg.TabIndex = 1;
            // 
            // button_sendMsg
            // 
            this.button_sendMsg.Location = new System.Drawing.Point(635, 171);
            this.button_sendMsg.Name = "button_sendMsg";
            this.button_sendMsg.Size = new System.Drawing.Size(75, 23);
            this.button_sendMsg.TabIndex = 2;
            this.button_sendMsg.Text = "Send";
            this.button_sendMsg.UseVisualStyleBackColor = true;
            this.button_sendMsg.Click += new System.EventHandler(this.button_sendMsg_Click);
            // 
            // label_msg
            // 
            this.label_msg.AutoSize = true;
            this.label_msg.Location = new System.Drawing.Point(81, 177);
            this.label_msg.Name = "label_msg";
            this.label_msg.Size = new System.Drawing.Size(50, 13);
            this.label_msg.TabIndex = 3;
            this.label_msg.Text = "Message";
            // 
            // label_file
            // 
            this.label_file.AutoSize = true;
            this.label_file.Location = new System.Drawing.Point(82, 220);
            this.label_file.Name = "label_file";
            this.label_file.Size = new System.Drawing.Size(48, 13);
            this.label_file.TabIndex = 6;
            this.label_file.Text = "File Path";
            // 
            // button_sendFile
            // 
            this.button_sendFile.Location = new System.Drawing.Point(635, 214);
            this.button_sendFile.Name = "button_sendFile";
            this.button_sendFile.Size = new System.Drawing.Size(75, 23);
            this.button_sendFile.TabIndex = 5;
            this.button_sendFile.Text = "Send";
            this.button_sendFile.UseVisualStyleBackColor = true;
            this.button_sendFile.Click += new System.EventHandler(this.button_sendFile_Click);
            // 
            // textBox_filePath
            // 
            this.textBox_filePath.Location = new System.Drawing.Point(138, 217);
            this.textBox_filePath.Name = "textBox_filePath";
            this.textBox_filePath.Size = new System.Drawing.Size(423, 20);
            this.textBox_filePath.TabIndex = 4;
            // 
            // button_fileSelect
            // 
            this.button_fileSelect.Location = new System.Drawing.Point(576, 215);
            this.button_fileSelect.Name = "button_fileSelect";
            this.button_fileSelect.Size = new System.Drawing.Size(31, 23);
            this.button_fileSelect.TabIndex = 9;
            this.button_fileSelect.Text = "...";
            this.button_fileSelect.UseVisualStyleBackColor = true;
            this.button_fileSelect.Click += new System.EventHandler(this.button_fileSelect_Click);
            // 
            // textBox_remoteFilePath
            // 
            this.textBox_remoteFilePath.Location = new System.Drawing.Point(138, 254);
            this.textBox_remoteFilePath.Name = "textBox_remoteFilePath";
            this.textBox_remoteFilePath.Size = new System.Drawing.Size(423, 20);
            this.textBox_remoteFilePath.TabIndex = 10;
            // 
            // button_download
            // 
            this.button_download.Location = new System.Drawing.Point(635, 254);
            this.button_download.Name = "button_download";
            this.button_download.Size = new System.Drawing.Size(75, 23);
            this.button_download.TabIndex = 11;
            this.button_download.Text = "Download";
            this.button_download.UseVisualStyleBackColor = true;
            this.button_download.Click += new System.EventHandler(this.button_download_Click);
            // 
            // label_RPath
            // 
            this.label_RPath.AutoSize = true;
            this.label_RPath.Location = new System.Drawing.Point(83, 259);
            this.label_RPath.Name = "label_RPath";
            this.label_RPath.Size = new System.Drawing.Size(48, 13);
            this.label_RPath.TabIndex = 12;
            this.label_RPath.Text = "File Path";
            // 
            // textBox_IP
            // 
            this.textBox_IP.Location = new System.Drawing.Point(138, 76);
            this.textBox_IP.Name = "textBox_IP";
            this.textBox_IP.Size = new System.Drawing.Size(225, 20);
            this.textBox_IP.TabIndex = 13;
            this.textBox_IP.Text = "127.0.0.1";
            // 
            // label_IP
            // 
            this.label_IP.AutoSize = true;
            this.label_IP.Location = new System.Drawing.Point(83, 79);
            this.label_IP.Name = "label_IP";
            this.label_IP.Size = new System.Drawing.Size(48, 13);
            this.label_IP.TabIndex = 14;
            this.label_IP.Text = "ServerIP";
            // 
            // label_port
            // 
            this.label_port.AutoSize = true;
            this.label_port.Location = new System.Drawing.Point(395, 79);
            this.label_port.Name = "label_port";
            this.label_port.Size = new System.Drawing.Size(57, 13);
            this.label_port.TabIndex = 15;
            this.label_port.Text = "ServerPort";
            // 
            // textBox_Port
            // 
            this.textBox_Port.Location = new System.Drawing.Point(458, 76);
            this.textBox_Port.Name = "textBox_Port";
            this.textBox_Port.Size = new System.Drawing.Size(103, 20);
            this.textBox_Port.TabIndex = 16;
            this.textBox_Port.Text = "8000";
            // 
            // Client
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.textBox_Port);
            this.Controls.Add(this.label_port);
            this.Controls.Add(this.label_IP);
            this.Controls.Add(this.textBox_IP);
            this.Controls.Add(this.label_RPath);
            this.Controls.Add(this.button_download);
            this.Controls.Add(this.textBox_remoteFilePath);
            this.Controls.Add(this.button_fileSelect);
            this.Controls.Add(this.label_file);
            this.Controls.Add(this.button_sendFile);
            this.Controls.Add(this.textBox_filePath);
            this.Controls.Add(this.label_msg);
            this.Controls.Add(this.button_sendMsg);
            this.Controls.Add(this.textBox_msg);
            this.Controls.Add(this.button_connect);
            this.Name = "Client";
            this.Text = "Client";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button button_connect;
        private System.Windows.Forms.TextBox textBox_msg;
        private System.Windows.Forms.Button button_sendMsg;
        private System.Windows.Forms.Label label_msg;
        private System.Windows.Forms.Label label_file;
        private System.Windows.Forms.Button button_sendFile;
        private System.Windows.Forms.TextBox textBox_filePath;
        private System.Windows.Forms.Button button_fileSelect;
        private System.Windows.Forms.TextBox textBox_remoteFilePath;
        private System.Windows.Forms.Button button_download;
        private System.Windows.Forms.Label label_RPath;
        private System.Windows.Forms.TextBox textBox_IP;
        private System.Windows.Forms.Label label_IP;
        private System.Windows.Forms.Label label_port;
        private System.Windows.Forms.TextBox textBox_Port;
    }
}

