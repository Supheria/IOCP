using Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Net
{
    public class FullHandlerSocketProtocol : BaseSocketProtocol
    {
        private int m_packetSize;
        private byte[] m_readBuffer;

        private string m_fileName;
        public string FileName { get { return m_fileName; } }
        private FileStream m_fileStream;
        private bool m_sendFile;
        private bool m_receFile;

        private Int64 receviedLength = 0;
        private Int64 receive_fileSize = 0;
        public FullHandlerSocketProtocol(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
            : base(asyncSocketServer, asyncSocketUserToken)
        {
            m_fileName = "";
            m_fileStream = null;
            m_sendFile = false;
            m_receFile = false;
            m_packetSize = 64 * 1024;
            lock (m_asyncSocketServer.FullHandlerSocketProtocolMgr)
            {
                m_asyncSocketServer.FullHandlerSocketProtocolMgr.Add(this);
            }
        }
        public override void Close()
        {
            base.Close();
            lock (m_asyncSocketServer)
            {
                m_asyncSocketServer.FullHandlerSocketProtocolMgr.Remove(this);
            }
        }
        /// <summary>
        /// 发送消息到客户端，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送不成功
        /// </summary>
        /// <param name="msg">消息</param>
        public void SendMessage(string msg)
        {
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddResponse();
            m_outgoingDataAssembler.AddCommand(ProtocolKey.Message);
            m_outgoingDataAssembler.AddSuccess();
            byte[] Buffer = Encoding.UTF8.GetBytes(msg);
            DoSendResult(Buffer, 0, Buffer.Length);
        }
        public override bool ProcessCommand(byte[] buffer, int offset, int count) //处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求，返回结果必须加入m_outgoingDataAssembler.AddResponse();
        {
            FullHandlerSocketCommand command = StrToCommand(m_incomingDataParser.Command);
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddResponse();
            m_outgoingDataAssembler.AddCommand(m_incomingDataParser.Command);
            if (!CheckLogined(command)) //检测登录
            {
                m_outgoingDataAssembler.AddFailure(ProtocolCode.UserHasLogined, "");
                return DoSendResult();
            }
            if (command == FullHandlerSocketCommand.Login)
                return DoLogin();
            else if (command == FullHandlerSocketCommand.Active)
                return DoActive();
            else if (command == FullHandlerSocketCommand.Message)
                return DoHandlerMessage(buffer, offset, count);
            else if (command == FullHandlerSocketCommand.Dir)
                return DoDir();
            else if (command == FullHandlerSocketCommand.FileList)
                return DoFileList();
            else if (command == FullHandlerSocketCommand.Download)
                return DoDownload();
            else if (command == FullHandlerSocketCommand.Upload)
                return DoUpload();
            else if (command == FullHandlerSocketCommand.SendFile)
                return DoSendFile();
            else if (command == FullHandlerSocketCommand.Data)
                return DoData(buffer, offset, count);
            else
            {
                ServerInstance.Logger.Error("Unknow command: " + m_incomingDataParser.Command);
                return false;
            }
        }
        public FullHandlerSocketCommand StrToCommand(string command)//关键代码
        {
            if (command.Equals(ProtocolKey.Active, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Active;
            else if (command.Equals(ProtocolKey.Login, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Login;
            else if (command.Equals(ProtocolKey.Message, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Message;
            else if (command.Equals(ProtocolKey.Dir, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Dir;
            else if (command.Equals(ProtocolKey.FileList, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.FileList;
            else if (command.Equals(ProtocolKey.Download, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Download;
            else if (command.Equals(ProtocolKey.Upload, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Upload;
            else if (command.Equals(ProtocolKey.SendFile,StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.SendFile;
            else if (command.Equals(ProtocolKey.Data, StringComparison.CurrentCultureIgnoreCase))
                return FullHandlerSocketCommand.Data;
            else
                return FullHandlerSocketCommand.None;
        }
        public bool DoSendFile()
        {
            m_outgoingDataAssembler.AddSuccess();
            return DoSendResult();
        }
        public bool DoData(byte[] buffer, int offset, int count)
        {            
            m_fileStream.Write(buffer, offset, count);
            receviedLength += count;
            if (receviedLength == receive_fileSize)
            {
                m_fileStream.Close();
                m_fileStream.Dispose();
                receviedLength = 0;
                m_receFile = false;
#if DEBUG
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("文件接收成功，完成时间{0}", DateTime.Now);
                Console.ForegroundColor = ConsoleColor.White;
#endif
            }
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddResponse();
            m_outgoingDataAssembler.AddCommand(ProtocolKey.Data);
            m_outgoingDataAssembler.AddSuccess();
            //m_outgoingDataAssembler.AddValue(ProtocolKey.FileSize, receive_fileSize - m_fileStream.Position);//将当前的文件流位置发给客户端
            return DoSendResult();
        }

        public bool DoUpload()//处理客户端文件上传
        {
            string dirName = "";
            string fileName = "";
            Int64 fileSize = 0;
            int packetSize = 0;
            if (m_incomingDataParser.GetValue(ProtocolKey.DirName, ref dirName) & m_incomingDataParser.GetValue(ProtocolKey.FileName, ref fileName) & m_incomingDataParser.GetValue(ProtocolKey.FileSize, ref fileSize) & m_incomingDataParser.GetValue(ProtocolKey.PacketSize, ref packetSize))
            {
                receive_fileSize = fileSize;
                if (dirName == "")
                    dirName = ServerInstance.FileDirectory;                
                fileName = Path.Combine(dirName, fileName);
                ServerInstance.Logger.Info("Start Receive file: " + fileName);
                if (m_fileStream != null) //关闭上次传输的文件
                {
                    m_fileStream.Close();
                    m_fileStream = null;
                    m_fileName = "";
                }
                if (File.Exists(fileName))//本地存在，则删除重建
                {
                    if (!CheckFileInUse(fileName)) //检测文件是否正在使用中
                    {
                        File.Delete(fileName);
                        m_fileName = fileName;
                        m_fileStream = new FileStream(m_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        m_outgoingDataAssembler.AddSuccess();
                        m_receFile = true;
                    }
                    else
                    {
                        m_outgoingDataAssembler.AddFailure(ProtocolCode.FileIsInUse, "");
                        ServerInstance.Logger.Error("Start Receive file error, file is in use: " + fileName);
                    }
                }
                else
                {
                    m_fileName = fileName;
                    m_fileStream = new FileStream(m_fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    m_outgoingDataAssembler.AddSuccess();
                    m_receFile = true;
                }
            }
            return DoSendResult();
        }

        private bool DoHandlerMessage(byte[] buffer, int offset, int count)
        {
            m_outgoingDataAssembler.AddSuccess();
            string msg = Encoding.UTF8.GetString(buffer, offset, count);
            //服务端在此处处理所有客户的消息请求
            if (ServerInstance.appHandler != null)
            {
                ServerInstance.appHandler.HandlerMsg(msg, this);
            }
            return DoSendResult();
        }
        
        public bool CheckLogined(FullHandlerSocketCommand command)
        {
            if ((command == FullHandlerSocketCommand.Login) | (command == FullHandlerSocketCommand.Active))
                return true;
            else
                return m_logined;
        }
        public new bool DoLogin()
        {
            string userID = "";
            string password = "";
            if (m_incomingDataParser.GetValue(ProtocolKey.UserID, ref userID) & m_incomingDataParser.GetValue(ProtocolKey.Password, ref password))
            {
                if (userID == "admin" && password == "password")
                {
                    m_outgoingDataAssembler.AddSuccess();
                    m_userID = "admin";
                    m_userName = "admin";
                    m_logined = true;
                    m_outgoingDataAssembler.AddValue(ProtocolKey.UserID, "admin");
                    m_outgoingDataAssembler.AddValue(ProtocolKey.UserName, "admin");
                    ServerInstance.Logger.InfoFormat("{0} login success", userID);
                }
            }
            else
            {
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
                ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userID);
            }
            return DoSendResult();
        }
        
        public bool DoDir()
        {
            string parentDir = "";
            if (m_incomingDataParser.GetValue(ProtocolKey.ParentDir, ref parentDir))
            {
                if (parentDir == "")
                    parentDir = ServerInstance.FileDirectory;
                else
                    parentDir = Path.Combine(ServerInstance.FileDirectory, parentDir);
                if (Directory.Exists(parentDir))
                {
                    string[] subDirectorys = Directory.GetDirectories(parentDir, "*", SearchOption.TopDirectoryOnly);
                    m_outgoingDataAssembler.AddSuccess();
                    char[] directorySeparator = new char[1];
                    directorySeparator[0] = Path.DirectorySeparatorChar;
                    for (int i = 0; i < subDirectorys.Length; i++)
                    {
                        string[] directoryName = subDirectorys[i].Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries);
                        m_outgoingDataAssembler.AddValue(ProtocolKey.Item, directoryName[directoryName.Length - 1]);
                    }
                }
                else
                    m_outgoingDataAssembler.AddFailure(ProtocolCode.DirNotExist, "");
            }
            else
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
            return DoSendResult();
        }

        public bool DoFileList()
        {
            string dirName = "";
            if (m_incomingDataParser.GetValue(ProtocolKey.DirName, ref dirName))
            {
                if (dirName == "")
                    dirName = ServerInstance.FileDirectory;
                else
                    dirName = Path.Combine(ServerInstance.FileDirectory, dirName);
                if (Directory.Exists(dirName))
                {
                    string[] files = Directory.GetFiles(dirName);
                    m_outgoingDataAssembler.AddSuccess();
                    Int64 fileSize = 0;
                    for (int i = 0; i < files.Length; i++)
                    {
                        FileInfo fileInfo = new FileInfo(files[i]);
                        fileSize = fileInfo.Length;
                        m_outgoingDataAssembler.AddValue(ProtocolKey.Item, fileInfo.Name + ProtocolKey.TextSeperator + fileSize.ToString());
                    }
                }
                else
                    m_outgoingDataAssembler.AddFailure(ProtocolCode.DirNotExist, "");
            }
            else
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
            return DoSendResult();
        }

        public bool DoDownload()//处理客户端文件下载
        {
            string dirName = "";
            string fileName = "";
            Int64 fileSize = 0;
            int packetSize = 0;
            if (m_incomingDataParser.GetValue(ProtocolKey.DirName, ref dirName) & m_incomingDataParser.GetValue(ProtocolKey.FileName, ref fileName)
                & m_incomingDataParser.GetValue(ProtocolKey.FileSize, ref fileSize) & m_incomingDataParser.GetValue(ProtocolKey.PacketSize, ref packetSize))
            {
                if (dirName == "")
                    dirName = ServerInstance.FileDirectory;
                else
                    dirName = Path.Combine(ServerInstance.FileDirectory, dirName);
                fileName = Path.Combine(dirName, fileName);
                ServerInstance.Logger.Info("Start download file: " + fileName);
                if (m_fileStream != null) //关闭上次传输的文件
                {
                    m_fileStream.Close();
                    m_fileStream = null;
                    m_fileName = "";
                    m_sendFile = false;
                }
                if (File.Exists(fileName))
                {
                    if (!CheckFileInUse(fileName)) //检测文件是否正在使用中
                    {
                        m_fileName = fileName;
                        m_fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);//文件以共享只读方式打开，方便多个客户端下载同一个文件。
                        m_fileStream.Position = fileSize; //文件移到上次下载位置                        
                        m_outgoingDataAssembler.AddSuccess();
                        m_sendFile = true;
                        m_packetSize = packetSize;
                    }
                    else
                    {
                        m_outgoingDataAssembler.AddFailure(ProtocolCode.FileIsInUse, "");
                        ServerInstance.Logger.Error("Start download file error, file is in use: " + fileName);
                    }
                }
                else
                {
                    m_outgoingDataAssembler.AddFailure(ProtocolCode.FileNotExist, "");
                }
            }
            else
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
            return DoSendResult();
        }
        //检测文件是否正在使用中，如果正在使用中则检测是否被上传协议占用，如果占用则关闭,真表示正在使用中，并没有关闭
        public bool CheckFileInUse(string fileName)
        {
            if (BasicFunc.IsFileInUse(fileName))
            {
                bool result = true;
                lock (m_asyncSocketServer.FullHandlerSocketProtocolMgr)
                {
                    FullHandlerSocketProtocol fullHandlerSocketProtocol = null;
                    for (int i = 0; i < m_asyncSocketServer.FullHandlerSocketProtocolMgr.Count(); i++)
                    {
                        fullHandlerSocketProtocol = m_asyncSocketServer.FullHandlerSocketProtocolMgr.ElementAt(i);
                        if (fileName.Equals(fullHandlerSocketProtocol.FileName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            lock (fullHandlerSocketProtocol.AsyncSocketUserToken) //AsyncSocketUserToken有多个线程访问
                            {
                                m_asyncSocketServer.CloseClientSocket(fullHandlerSocketProtocol.AsyncSocketUserToken);
                            }
                            result = false;
                        }
                    }
                }
                return result;
            }
            else
                return false;
        }
        public override bool SendCallback()
        {
            bool result = base.SendCallback();
            if (m_fileStream != null)
            {
                if (m_sendFile) //发送文件头
                {
                    m_outgoingDataAssembler.Clear();
                    m_outgoingDataAssembler.AddResponse();
                    m_outgoingDataAssembler.AddCommand(ProtocolKey.SendFile);
                    m_outgoingDataAssembler.AddSuccess();
                    m_outgoingDataAssembler.AddValue(ProtocolKey.FileSize, m_fileStream.Length - m_fileStream.Position);
                    result = DoSendResult();
                    m_sendFile = false;
                }
                else if (!m_receFile)//没有接收文件时
                {
                    if (m_fileStream.CanSeek && m_fileStream.Position < m_fileStream.Length) //发送具体数据,加m_fileStream.CanSeek是防止上传文件结束后，文件流被释放而出错
                    {
                        m_outgoingDataAssembler.Clear();
                        m_outgoingDataAssembler.AddResponse();
                        m_outgoingDataAssembler.AddCommand(ProtocolKey.Data);
                        m_outgoingDataAssembler.AddSuccess();
                        if (m_readBuffer == null)
                            m_readBuffer = new byte[m_packetSize];
                        else if (m_readBuffer.Length < m_packetSize) //避免多次申请内存
                            m_readBuffer = new byte[m_packetSize];
                        int count = m_fileStream.Read(m_readBuffer, 0, m_packetSize);
                        result = DoSendResult(m_readBuffer, 0, count);
                    }
                    else //发送完成
                    {
                        ServerInstance.Logger.Info("End Upload file: " + m_fileName);
                        m_fileStream.Close();
                        m_fileStream = null;
                        m_fileName = "";
                        m_sendFile = false;
                        result = true;
                    }
                }
            }
            return result;
        }
    }
    public class FullHandlerSocketProtocolMgr : Object
    {
        private List<FullHandlerSocketProtocol> m_list;

        public FullHandlerSocketProtocolMgr()
        {
            m_list = new List<FullHandlerSocketProtocol>();
        }

        public int Count()
        {
            return m_list.Count;
        }

        public FullHandlerSocketProtocol ElementAt(int index)
        {
            return m_list.ElementAt(index);
        }

        public void Add(FullHandlerSocketProtocol value)
        {
            m_list.Add(value);
        }

        public void Remove(FullHandlerSocketProtocol value)
        {
            m_list.Remove(value);
        }
        /// <summary>
        /// 向在线的客户端广播
        /// </summary>
        /// <param name="msg">广播信息</param>
        public void Broadcast(string msg)
        {
            foreach (var item in m_list)
            {
                ((FullHandlerSocketProtocol)item).SendMessage(msg);
            }
        }
    }
}
