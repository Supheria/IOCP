using Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Net;

public class ServerFullHandlerProtocol(IocpServer server, AsyncUserToken userToken) : IocpServerProtocol(IocpProtocolTypes.FullHandler, server, userToken)
{
    enum Command
    {
        None = 0,
        Login = 1,
        Active = 2,
        Dir = 3,
        FileList = 4,
        Download = 5,
        Data = 6,
        Message = 7,
        Upload = 8,
        SendFile = 9
    }

    int PacketSize { get; set; } = 64 * 1024;


    byte[]? ReadBuffer { get; set; } = null;

    public string FilePath { get; private set; } = "";

    FileStream? FileStream { get; set; } = null;

    bool IsSendingFile { get; set; } = false;

    bool IsReceivingFile { get; set; } = false;

    long ReceviedLength { get; set; } = 0;

    long ReceivedFileSize { get; set; } = 0;

    public DirectoryInfo RootDirectory { get; set; } = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "upload"));

    public string RootDirectoryPath => RootDirectory.FullName;

    public override void Dispose()
    {
        FilePath = "";
        FileStream?.Close();
        FileStream = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 发送消息到客户端，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送不成功
    /// </summary>
    /// <param name="msg">消息</param>
    public void SendMessage(string msg)
    {
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(ProtocolKey.Message);
        CommandComposer.AddSuccess();
        byte[] Buffer = Encoding.UTF8.GetBytes(msg);
        DoSendResult(Buffer, 0, Buffer.Length);
    }
    public override bool ProcessCommand(byte[] buffer, int offset, int count) //处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求，返回结果必须加入m_outgoingDataAssembler.AddResponse();
    {
        Command command = StrToCommand(CommandParser.Command);
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(CommandParser.Command);
        if (!CheckLogined(command)) //检测登录
        {
            CommandComposer.AddFailure(ProtocolCode.UserHasLogined, "");
            return DoSendResult();
        }
        if (command == Command.Login)
            return DoLogin();
        else if (command == Command.Active)
            return DoActive();
        else if (command == Command.Message)
            return DoHandlerMessage(buffer, offset, count);
        else if (command == Command.Dir)
            return DoDir();
        else if (command == Command.FileList)
            return DoFileList();
        else if (command == Command.Download)
            return DoDownload();
        else if (command == Command.Upload)
            return DoUpload();
        else if (command == Command.SendFile)
            return DoSendFile();
        else if (command == Command.Data)
            return DoData(buffer, offset, count);
        else
        {
            //ServerInstance.Logger.Error("Unknow command: " + CommandParser.Command);
            return false;
        }
    }

    private Command StrToCommand(string command)//关键代码
    {
        if (command.Equals(ProtocolKey.Active, StringComparison.CurrentCultureIgnoreCase))
            return Command.Active;
        else if (command.Equals(ProtocolKey.Login, StringComparison.CurrentCultureIgnoreCase))
            return Command.Login;
        else if (command.Equals(ProtocolKey.Message, StringComparison.CurrentCultureIgnoreCase))
            return Command.Message;
        else if (command.Equals(ProtocolKey.Dir, StringComparison.CurrentCultureIgnoreCase))
            return Command.Dir;
        else if (command.Equals(ProtocolKey.FileList, StringComparison.CurrentCultureIgnoreCase))
            return Command.FileList;
        else if (command.Equals(ProtocolKey.Download, StringComparison.CurrentCultureIgnoreCase))
            return Command.Download;
        else if (command.Equals(ProtocolKey.Upload, StringComparison.CurrentCultureIgnoreCase))
            return Command.Upload;
        else if (command.Equals(ProtocolKey.SendFile,StringComparison.CurrentCultureIgnoreCase))
            return Command.SendFile;
        else if (command.Equals(ProtocolKey.Data, StringComparison.CurrentCultureIgnoreCase))
            return Command.Data;
        else
            return Command.None;
    }
    public bool DoSendFile()
    {
        CommandComposer.AddSuccess();
        return DoSendResult();
    }
    public bool DoData(byte[] buffer, int offset, int count)
    {            
        FileStream.Write(buffer, offset, count);
        ReceviedLength += count;
        if (ReceviedLength == ReceivedFileSize)
        {
            FileStream.Close();
            FileStream.Dispose();
            ReceviedLength = 0;
            IsReceivingFile = false;
#if DEBUG
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("文件接收成功，完成时间{0}", DateTime.Now);
            Console.ForegroundColor = ConsoleColor.White;
#endif
        }
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(ProtocolKey.Data);
        CommandComposer.AddSuccess();
        //CommandComposer.AddValue(ProtocolKey.FileSize, ReceivedFileSize - FileStream.Position);//将当前的文件流位置发给客户端
        return DoSendResult();
    }

    public bool DoUpload()//处理客户端文件上传
    {
        string dirName = "";
        string fileName = "";
        Int64 fileSize = 0;
        int packetSize = 0;
        if (CommandParser.GetValue(ProtocolKey.DirName, ref dirName) & CommandParser.GetValue(ProtocolKey.FileName, ref fileName) & CommandParser.GetValue(ProtocolKey.FileSize, ref fileSize) & CommandParser.GetValue(ProtocolKey.PacketSize, ref packetSize))
        {
            ReceivedFileSize = fileSize;
            if (dirName == "")
                dirName = RootDirectoryPath;
            fileName = Path.Combine(dirName, fileName);
            //ServerInstance.Logger.Info("Start Receive file: " + fileName);
            if (FileStream != null) //关闭上次传输的文件
            {
                FileStream.Close();
                FileStream = null;
                FilePath = "";
            }
            if (File.Exists(fileName))//本地存在，则删除重建
            {
                if (!CheckFileInUse(fileName)) //检测文件是否正在使用中
                {
                    File.Delete(fileName);
                    FilePath = fileName;
                    FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    CommandComposer.AddSuccess();
                    IsReceivingFile = true;
                }
                else
                {
                    CommandComposer.AddFailure(ProtocolCode.FileIsInUse, "");
                    //ServerInstance.Logger.Error("Start Receive file error, file is in use: " + fileName);
                }
            }
            else
            {
                FilePath = fileName;
                FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                CommandComposer.AddSuccess();
                IsReceivingFile = true;
            }
        }
        return DoSendResult();
    }

    private bool DoHandlerMessage(byte[] buffer, int offset, int count)
    {
        var message = Encoding.UTF8.GetString(buffer, offset, count);
        UserToken.Server.HandleReceiveMessage(message, this);
        CommandComposer.AddSuccess();
        return DoSendResult();
    }
    
    private bool CheckLogined(Command command)
    {
        if ((command == Command.Login) | (command == Command.Active))
            return true;
        else
            return IsLogin;
    }
    public new bool DoLogin()
    {
        string userID = "";
        string password = "";
        if (CommandParser.GetValue(ProtocolKey.UserID, ref userID) & CommandParser.GetValue(ProtocolKey.Password, ref password))
        {
            if (userID == "admin" && password == "password")
            {
                CommandComposer.AddSuccess();
                UserID = "admin";
                UserName = "admin";
                IsLogin = true;
                CommandComposer.AddValue(ProtocolKey.UserID, "admin");
                CommandComposer.AddValue(ProtocolKey.UserName, "admin");
                //ServerInstance.Logger.InfoFormat("{0} login success", userID);
            }
        }
        else
        {
            CommandComposer.AddFailure(ProtocolCode.ParameterError, "");
            //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userID);
        }
        return DoSendResult();
    }
    
    public bool DoDir()
    {
        string parentDir = "";
        if (CommandParser.GetValue(ProtocolKey.ParentDir, ref parentDir))
        {
            if (parentDir == "")
                parentDir = RootDirectoryPath;
            else
                parentDir = Path.Combine(RootDirectoryPath, parentDir);
            if (Directory.Exists(parentDir))
            {
                string[] subDirectorys = Directory.GetDirectories(parentDir, "*", SearchOption.TopDirectoryOnly);
                CommandComposer.AddSuccess();
                char[] directorySeparator = new char[1];
                directorySeparator[0] = Path.DirectorySeparatorChar;
                for (int i = 0; i < subDirectorys.Length; i++)
                {
                    string[] directoryName = subDirectorys[i].Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries);
                    CommandComposer.AddValue(ProtocolKey.Item, directoryName[directoryName.Length - 1]);
                }
            }
            else
                CommandComposer.AddFailure(ProtocolCode.DirNotExist, "");
        }
        else
            CommandComposer.AddFailure(ProtocolCode.ParameterError, "");
        return DoSendResult();
    }

    public bool DoFileList()
    {
        string dirName = "";
        if (CommandParser.GetValue(ProtocolKey.DirName, ref dirName))
        {
            if (dirName == "")
                dirName = RootDirectoryPath;
            else
                dirName = Path.Combine(RootDirectoryPath, dirName);
            if (Directory.Exists(dirName))
            {
                string[] files = Directory.GetFiles(dirName);
                CommandComposer.AddSuccess();
                Int64 fileSize = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    FileInfo fileInfo = new FileInfo(files[i]);
                    fileSize = fileInfo.Length;
                    CommandComposer.AddValue(ProtocolKey.Item, fileInfo.Name + ProtocolKey.TextSeperator + fileSize.ToString());
                }
            }
            else
                CommandComposer.AddFailure(ProtocolCode.DirNotExist, "");
        }
        else
            CommandComposer.AddFailure(ProtocolCode.ParameterError, "");
        return DoSendResult();
    }

    public bool DoDownload()//处理客户端文件下载
    {
        string dirName = "";
        string fileName = "";
        Int64 fileSize = 0;
        int packetSize = 0;
        if (CommandParser.GetValue(ProtocolKey.DirName, ref dirName) & CommandParser.GetValue(ProtocolKey.FileName, ref fileName)
            & CommandParser.GetValue(ProtocolKey.FileSize, ref fileSize) & CommandParser.GetValue(ProtocolKey.PacketSize, ref packetSize))
        {
            if (dirName == "")
                dirName = RootDirectoryPath;
            else
                dirName = Path.Combine(RootDirectoryPath, dirName);
            fileName = Path.Combine(dirName, fileName);
            //ServerInstance.Logger.Info("Start download file: " + fileName);
            if (FileStream != null) //关闭上次传输的文件
            {
                FileStream.Close();
                FileStream = null;
                FilePath = "";
                IsSendingFile = false;
            }
            if (File.Exists(fileName))
            {
                if (!CheckFileInUse(fileName)) //检测文件是否正在使用中
                {
                    FilePath = fileName;
                    FileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);//文件以共享只读方式打开，方便多个客户端下载同一个文件。
                    FileStream.Position = fileSize; //文件移到上次下载位置                        
                    CommandComposer.AddSuccess();
                    IsSendingFile = true;
                    PacketSize = packetSize;
                }
                else
                {
                    CommandComposer.AddFailure(ProtocolCode.FileIsInUse, "");
                    //ServerInstance.Logger.Error("Start download file error, file is in use: " + fileName);
                }
            }
            else
            {
                CommandComposer.AddFailure(ProtocolCode.FileNotExist, "");
            }
        }
        else
            CommandComposer.AddFailure(ProtocolCode.ParameterError, "");
        return DoSendResult();
    }
    //检测文件是否正在使用中，如果正在使用中则检测是否被上传协议占用，如果占用则关闭,真表示正在使用中，并没有关闭
    public bool CheckFileInUse(string filePath)
    {
        if (isFileInUse())
        {
            bool result = true;
            Server.UserTokenList.CopyTo(out var userTokens);
            foreach (var userToken in userTokens)
            {
                if (userToken.Protocol is not ServerFullHandlerProtocol fullHandler)
                    continue;
                if (!filePath.Equals(fullHandler.FilePath, StringComparison.CurrentCultureIgnoreCase))
                    continue;
                //lock (userToken) //AsyncSocketUserToken有多个线程访问
                userToken.Close();
                result = false;
            }
            return result;
            //lock (Server.FullHandlerSocketProtocolMgr)
            //{
            //    ServerFullHandlerProtocol fullHandlerSocketProtocol = null;
            //    for (int i = 0; i < Server.FullHandlerSocketProtocolMgr.Count(); i++)
            //    {
            //        fullHandlerSocketProtocol = Server.FullHandlerSocketProtocolMgr.ElementAt(i);
            //        if (filePath.Equals(fullHandlerSocketProtocol.FilePath, StringComparison.CurrentCultureIgnoreCase))
            //        {
            //            lock (fullHandlerSocketProtocol.UserToken) //AsyncSocketUserToken有多个线程访问
            //            {
            //                Server.CloseClientSocket(fullHandlerSocketProtocol.UserToken);
            //            }
            //            result = false;
            //        }
            //    }
            //}
            //return result;
        }
        else
            return false;
        bool isFileInUse()
        {
            try
            {
                // 使用共享只读方式打开，可以支持多个客户端同时访问一个文件。
                using var _ = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
    public override bool SendCallback()
    {
        bool result = base.SendCallback();
        if (FileStream != null)
        {
            if (IsSendingFile) //发送文件头
            {
                CommandComposer.Clear();
                CommandComposer.AddResponse();
                CommandComposer.AddCommand(ProtocolKey.SendFile);
                CommandComposer.AddSuccess();
                CommandComposer.AddValue(ProtocolKey.FileSize, FileStream.Length - FileStream.Position);
                result = DoSendResult();
                IsSendingFile = false;
            }
            else if (!IsReceivingFile)//没有接收文件时
            {
                if (FileStream.CanSeek && FileStream.Position < FileStream.Length) //发送具体数据,加m_fileStream.CanSeek是防止上传文件结束后，文件流被释放而出错
                {
                    CommandComposer.Clear();
                    CommandComposer.AddResponse();
                    CommandComposer.AddCommand(ProtocolKey.Data);
                    CommandComposer.AddSuccess();
                    if (ReadBuffer == null)
                        ReadBuffer = new byte[PacketSize];
                    else if (ReadBuffer.Length < PacketSize) //避免多次申请内存
                        ReadBuffer = new byte[PacketSize];
                    int count = FileStream.Read(ReadBuffer, 0, PacketSize);
                    result = DoSendResult(ReadBuffer, 0, count);
                }
                else //发送完成
                {
                    //ServerInstance.Logger.Info("End Upload file: " + FilePath);
                    FileStream.Close();
                    FileStream = null;
                    FilePath = "";
                    IsSendingFile = false;
                    result = true;
                }
            }
        }
        return result;
    }
}
public class FullHandlerSocketProtocolMgr : Object
{
    private List<ServerFullHandlerProtocol> m_list;

    public FullHandlerSocketProtocolMgr()
    {
        m_list = new List<ServerFullHandlerProtocol>();
    }

    public int Count()
    {
        return m_list.Count;
    }

    public ServerFullHandlerProtocol ElementAt(int index)
    {
        return m_list.ElementAt(index);
    }

    public void Add(ServerFullHandlerProtocol value)
    {
        m_list.Add(value);
    }

    public void Remove(ServerFullHandlerProtocol value)
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
            ((ServerFullHandlerProtocol)item).SendMessage(msg);
        }
    }
}
