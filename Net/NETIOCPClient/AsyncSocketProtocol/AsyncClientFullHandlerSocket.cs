using NETIOCPClient.AsyncSocketProtocolCore;
using Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using log4net;
using Net;

namespace NETIOCPClient.AsyncSocketProtocol
{
    public class StateObject
    {        
        public Socket workSocket = null;
    }
    public static class StaticResetevent
    {
        public static AutoResetEvent Done = new AutoResetEvent(false);
    }
    public class DownLoadPara
    {
        public string dir = "";
        public string name = "";
        public string filetype { get; set; }
    }
    public class DownloadEvent
    {
        public List<DownLoadPara> ListQueue;
        public delegate void DownLoadProcess();
        public event DownLoadProcess downLoadProcess;
        public void OnProcessDownLoad()
        {
            if (downLoadProcess != null)
            {
                downLoadProcess();
            }
        }
        public DownloadEvent()
        {
            ListQueue = new List<DownLoadPara>();
        }
    }
    public class UploadEvent
    {
        public delegate void UploadProcess();
        public event UploadProcess uploadProcess;
        public void OnProcessUpload()
        {
            if (uploadProcess != null)
            {
                uploadProcess();
            }
        }        
    }
    
    public class AsyncClientFullHandlerSocket : AsyncClientBaseSocket
    {
        private int packetLength = 0;
        private int packetReceived = 0;
        public UserInfo loginUser = new();

        private string Password = "";
        private bool BnetWorkOperate = false;
        public static int PacketSize = 8 * 1024;
        private string m_fileName;
        public Int64 FileSize = 0;//文件的剩余长度
        private Int64 receviedLength = 0;//本次文件已经接收的长度
        private string m_localFilePath;//本地保存文件的路径,不含文件名
        public string localFilePath { set { m_localFilePath = value; } get { return m_localFilePath; } }
        public string LocalIp { get { return ((IPEndPoint)m_tcpClient.client.LocalEndPoint).Address.ToString(); } }
        public string FileName { get { return m_fileName; } }
        private FileStream m_fileStream;
        private bool m_sendFile;
        private byte[] m_readBuffer;
        private DownloadEvent DE { get; }//下载完成后的事件
        private UploadEvent UE;//上传完成后的事件
        public AppHandler appHandler;//接收到消息后的处理事件
        public ILog logger;

        public AsyncClientFullHandlerSocket(DownloadEvent de,UploadEvent ue)
            : base()
        {
            logger = Logger;//继承自父对象
            m_protocolFlag = IocpProtocolTypes.FullHandler;
            m_fileName = "";
            m_fileStream = null;
            DE = de;
            UE = ue;
        }
        /// <summary>
        /// 向服务端发送消息，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送失败
        /// </summary>
        /// <param name="msg">消息内容</param>
        public void SendMessage(string msg)
        {
            bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
            if (!bConnect)
            {
                Logger.Error("AsyncClientFullHandlerSocket.SendMessage:" + "Socket disconnect");
                throw new Exception("Server is Stoped"); //抛异常是为了让前台知道网络链路的情况         
            }
            else
            {
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(ProtocolKey.Message);
                byte[] bufferMsg = Encoding.UTF8.GetBytes(msg);
                SendCommand(bufferMsg, 0, bufferMsg.Length);
            }
        }
        public void SendMessageQuick(string msg)
        {
            try
            {
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(ProtocolKey.Message);
                byte[] bufferMsg = Encoding.UTF8.GetBytes(msg);
                SendCommand(bufferMsg, 0, bufferMsg.Length);
            }
            catch (Exception e)
            {
                logger.Error("SendMessage error:" + e.Message);
                bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
                if (!bConnect)
                {
                    Logger.Error("AsyncClientFullHandlerSocket.SendMessage:" + "Socket disconnect");
                    throw new Exception("Server is Stoped"); //抛异常是为了让前台知道网络链路的情况         
                }
                else
                    SendMessageQuick(msg);
            }
        }
        /// <summary>
        /// 循环接收消息
        /// </summary>
        public void ReceiveMessageHead()
        {
            StateObject state = new StateObject();
            state.workSocket = m_tcpClient.client;
            m_tcpClient.client.BeginReceive(m_recvBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);
        }
        public void ReceiveMessageHeadCallBack(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                var socket = state.workSocket;
                var length = socket.EndReceive(ar);
                if (length == 0)//接收到0字节表示Socket正常断开
                {
                    Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageHeadCallBack:" + "Socket disconnect");
                    return;
                }
                if (length < sizeof(int))//小于四个字节表示包头未完全接收，继续接收
                {
                    m_tcpClient.client.BeginReceive(m_recvBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);
                    return;
                }
                packetLength = BitConverter.ToInt32(m_recvBuffer.Buffer, 0); //获取包长度     
                if (NetByteOrder)
                    packetLength = IPAddress.NetworkToHostOrder(packetLength); //把网络字节顺序转为本地字节顺序
                m_recvBuffer.SetBufferSize(sizeof(int) + packetLength); //保证接收有足够的空间
                socket.BeginReceive(m_recvBuffer.Buffer, sizeof(int), packetLength - sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageDataCallback), state);//每一次异步接收数据都挂接一个新的回调方法，保证一对一
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageHeadCallBack:" + ex.Message);
            }
        }
        private void ReceiveMessageDataCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
                int bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                {
                    packetReceived += bytesRead;
                    if (packetReceived + sizeof(int) == packetLength)//整个包都接收完成后操作
                    {
                        packetReceived = 0;
                        int size = 0;
                        int commandLen = BitConverter.ToInt32(m_recvBuffer.Buffer, sizeof(int)); //取出命令长度
                        string tmpStr = Encoding.UTF8.GetString(m_recvBuffer.Buffer, sizeof(int) + sizeof(int), commandLen);
                        if (m_incomingDataParser.DecodeProtocolText(tmpStr)) //解析命令，命令（除Message）完成后，必须要使用StaticResetevent这个静态信号量，保证同一时刻只有一个命令在执行
                        {
                            if (m_incomingDataParser.Command.Equals(ProtocolKey.Active, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckErrorCode())
                                {
                                    BnetWorkOperate = true;
                                }
                                else
                                    BnetWorkOperate = false;
                            }
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.Message, StringComparison.CurrentCultureIgnoreCase))
                            {
                                int offset = commandLen + sizeof(int) + sizeof(int);//前8个字节为包长度+命令长度
                                size = packetLength - offset;
                                string msg = Encoding.UTF8.GetString(m_recvBuffer.Buffer, offset, size);
#if DEBUG
                                if (msg != string.Empty)
                                    Console.WriteLine("Message Recevied from Server: " + msg);
#endif
                                //DoHandleMessage
                                if (!string.IsNullOrWhiteSpace(msg))
                                {
                                    if (appHandler != null)
                                    {
                                        appHandler.HandlerMsg(msg);//将业务逻辑引出到框架外部
                                    }
                                }
                            }
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.Login, StringComparison.CurrentCultureIgnoreCase))//登录信息返回
                            {
                                if (CheckErrorCode())//返回登录成功
                                {
                                    m_userID = m_incomingDataParser.Values[1];
                                    m_userName = m_incomingDataParser.Values[2];                                    
                                    loginUser.Id = m_userID;
                                    loginUser.Password = Password;
                                    loginUser.Name = m_userName;                                    
                                    BnetWorkOperate = true;
                                }
                                else
                                {
                                    BnetWorkOperate = false;
                                }
                                StaticResetevent.Done.Set();//登录结束
                            }                            
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.Download, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckErrorCode())//文件在服务端是否在使用、是否存在
                                {
                                    if (!File.Exists(m_fileName))//本地不存在，则创建
                                    {
                                        string dir = m_fileName.Substring(0, m_fileName.LastIndexOf("\\"));
                                        if (!Directory.Exists(dir))
                                        {
                                            Directory.CreateDirectory(dir);
                                        }
                                        m_fileStream = new FileStream(m_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                                    }
                                }
                            }
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.SendFile, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckErrorCode())
                                {
                                    if (m_sendFile)//上传文件中
                                    {
                                        if (m_fileStream.Position < m_fileStream.Length) //发送具体数据
                                        {
                                            m_outgoingDataAssembler.Clear();
                                            m_outgoingDataAssembler.AddRequest();
                                            m_outgoingDataAssembler.AddCommand(ProtocolKey.Data);

                                            if (m_readBuffer == null)
                                                m_readBuffer = new byte[PacketSize];
                                            int count = m_fileStream.Read(m_readBuffer, 0, PacketSize);
                                            SendCommand(m_readBuffer, 0, count);
                                        }             
                                    }
                                    else
                                    {
                                        m_incomingDataParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize);
                                        FileSize = fileSize;
                                    }
                                }
                            }
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.Data, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckErrorCode())
                                {
                                    if (m_sendFile)//上传文件中
                                    {                                        
                                        if (m_fileStream.Position < m_fileStream.Length) //发送具体数据
                                        {
                                            m_outgoingDataAssembler.Clear();
                                            m_outgoingDataAssembler.AddRequest();
                                            m_outgoingDataAssembler.AddCommand(ProtocolKey.Data);
                                            
                                            if (m_readBuffer == null)
                                                m_readBuffer = new byte[PacketSize];
                                            int count = m_fileStream.Read(m_readBuffer, 0, PacketSize);//读取剩余文件数据
                                            SendCommand(m_readBuffer, 0, count);
                                        }
                                        else //发送文件数据结束
                                        {
                                            m_sendFile = false;
                                            StaticResetevent.Done.Set();//上传结束 
                                            PacketSize = PacketSize / 8;//文件传输时将包大小放大8倍,传输完成后还原为原来大小
                                            UE.OnProcessUpload();
                                        }
                                    }
                                    else//下载文件
                                    {
                                        if (m_fileStream == null)
                                            m_fileStream = new FileStream(m_fileName, FileMode.Open, FileAccess.ReadWrite);
                                        m_fileStream.Position = m_fileStream.Length; //文件移到末尾                            
                                        int offset = commandLen + sizeof(int) + sizeof(int);//前8个字节为包长度+命令长度
                                        size = packetLength - offset;
                                        m_fileStream.Write(m_recvBuffer.Buffer, offset, size);
                                        receviedLength += size;
                                        if (receviedLength >= FileSize)
                                        {
                                            m_fileStream.Close();
                                            m_fileStream.Dispose();
                                            receviedLength = 0;
#if DEBUG
                                            Console.ForegroundColor = ConsoleColor.Green;
                                            Console.WriteLine("文件下载成功，完成时间{0}", DateTime.Now);
                                            Console.ForegroundColor = ConsoleColor.White;
#endif

                                            StaticResetevent.Done.Set();//下载完成
                                            if (DE != null)
                                            {
                                                DE.OnProcessDownLoad();
                                            }
                                        }
                                    }
                                }
                            }
                            else if (m_incomingDataParser.Command.Equals(ProtocolKey.Upload, StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (CheckErrorCode())
                                {
                                    if (m_fileStream != null)
                                    {
                                        if (m_sendFile) 
                                        {
                                            m_outgoingDataAssembler.Clear();
                                            m_outgoingDataAssembler.AddRequest();
                                            m_outgoingDataAssembler.AddCommand(ProtocolKey.SendFile);                                            
                                            //CommandComposer.AddValue(ProtocolKey.FileSize, FileStream.Length);
                                            SendCommand();                                            
                                        }                                        
                                    }
                                }
                            }
                            else
                            {
#if DEBUG
                                Console.WriteLine(tmpStr);
#endif
                                Logger.Warn("Unknown Command:" + tmpStr);
                            }
                        }
                        try
                        {                            
                            //判断client.Connected不准确，所以不要使用这个来判断连接是否正常
                            client.BeginReceive(m_recvBuffer.Buffer, 0, sizeof(int), SocketFlags.None, new AsyncCallback(ReceiveMessageHeadCallBack), state);//继续等待执行接收任务，实现消息循环
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message);
                            //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
                            throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
                        }
                    }
                    else//未接收完整个包数据则继续接收
                    {
                        int resDataLength = packetLength - packetReceived - sizeof(int);
                        try
                        {
                            client.BeginReceive(m_recvBuffer.Buffer, sizeof(int) + packetReceived, resDataLength, SocketFlags.None, new AsyncCallback(ReceiveMessageDataCallback), state);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex.Message);
                            //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
                            throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
                        }
                    }
                }
                else//接收到0字节表示Socket正常断开
                {
                    Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageDataCallback:" + "Socket disconnected");
                }
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine(e.ToString());
#endif
                Logger.Error("AsyncClientFullHandlerSocket.ReceiveMessageDataCallback:" + e.Message);
            }
        }
        public new bool DoLogin(string userID, string password)
        {            
            try
            {
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(ProtocolKey.Login);
                m_outgoingDataAssembler.AddValue(ProtocolKey.UserID, userID);
                //CommandComposer.AddValue(ProtocolKey.Password, IocpServer.BasicFunc.MD5String(password));
                m_outgoingDataAssembler.AddValue(ProtocolKey.Password, password);
                Password = password;
                SendCommand();
                StaticResetevent.Done.WaitOne();//登录阻塞，强制同步
                return BnetWorkOperate;
            }
            catch (Exception E)
            {
                //记录日志
                m_errorString = E.Message;
                Logger.Error("AsyncClientFullHandlerSocket.DoLogin" + "userID:" + userID + " password:" + password + " " + E.Message);
                return false;
            }
        }
        public new bool ReConnectAndLogin()//重新定义，防止使用基类的方法
        {
            if (BasicFunc.SocketConnected(m_tcpClient.client) && (DoActive()))
                return true;
            else
            {
                if (!BasicFunc.SocketConnected(m_tcpClient.client))
                {
                    try
                    {
                        Disconnect();
                        Connect(m_host, m_port);
                        ReceiveMessageHead();
                        return DoLogin(m_userID, Password);
                    }
                    catch (Exception E)
                    {
                        Logger.Error("AsyncClientFullHandlerSocket.ReConnectAndLogin" + "userID:" + m_userID + " password:" + Password + " " + E.Message);
                        return false;
                    }
                }
                else
                    return true;
            }
        }

        #region 文件下载
        public void DoDownload(string dirName, string fileName, string pathLastLevel)
        {
            bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
            if (!bConnect)
            {
                Logger.Error("<DoDownload>ClientFullHandlerSocket连接断开,并且无法重连");
                return;
            }
            try
            {
                long fileSize = 0;
                m_fileName = Path.Combine(m_localFilePath + pathLastLevel, fileName);
                if (File.Exists(m_fileName))//支持断点续传，如果有未下载完成的，则接着下载
                {
                    if (!BasicFunc.IsFileInUse(m_fileName)) //检测文件是否正在使用中
                    {
                        m_fileStream = new FileStream(m_fileName, FileMode.Open, FileAccess.ReadWrite);
                    }
                    else
                    {
                        Logger.Error("Start download file error, file is in use: " + fileName);
                        return;
                    }
                    fileSize = m_fileStream.Length;
                }
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(ProtocolKey.Download);
                m_outgoingDataAssembler.AddValue(ProtocolKey.DirName, dirName);
                m_outgoingDataAssembler.AddValue(ProtocolKey.FileName, fileName);
                m_outgoingDataAssembler.AddValue(ProtocolKey.FileSize, fileSize);
                m_outgoingDataAssembler.AddValue(ProtocolKey.PacketSize, PacketSize);
                SendCommand();
            }
            catch (Exception E)
            {
                //记录日志  
                m_errorString = E.Message;
                Logger.Error(E.Message);
            }
        }
        #endregion
        #region 文件上传
        public void DoUpload(string fileFullPath, string remoteDir, string remoteName)
        {
            bool bConnect = ReConnectAndLogin(); //检测连接是否还在，如果断开则重连并登录
            if (!bConnect)
            {
                Logger.Error("<Upload>ClientFullHandlerSocket连接断开,并且无法重连");
                return;
            }
            try
            {
                long fileSize = 0;
                if (File.Exists(fileFullPath))
                {
                    m_fileStream = new FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);//文件以共享只读方式打开
                    fileSize = m_fileStream.Length;
                    m_sendFile = true;
                    PacketSize = PacketSize * 8;//文件传输时设置包大小为原来的8倍，提高传输效率，传输完成后复原
                }
                else
                {
                    Logger.Error("Start Upload file error, file is not exists: " + fileFullPath);
                    return;
                }
                m_outgoingDataAssembler.Clear();
                m_outgoingDataAssembler.AddRequest();
                m_outgoingDataAssembler.AddCommand(ProtocolKey.Upload);
                m_outgoingDataAssembler.AddValue(ProtocolKey.DirName, remoteDir);
                m_outgoingDataAssembler.AddValue(ProtocolKey.FileName, remoteName);
                m_outgoingDataAssembler.AddValue(ProtocolKey.FileSize, fileSize);
                m_outgoingDataAssembler.AddValue(ProtocolKey.PacketSize, PacketSize);
                SendCommand();
            }
            catch (Exception e)
            {
                //记录日志  
                m_errorString = e.Message;
                Logger.Error(e.Message);
            }
        }
        #endregion
    }
}
