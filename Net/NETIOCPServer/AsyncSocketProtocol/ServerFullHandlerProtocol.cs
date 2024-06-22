using Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Net;

/// <summary>
/// 全功能处理协议
/// </summary>
/// <param name="server"></param>
/// <param name="userToken"></param>
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

    // TODO: make the dir more common-useable
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
        SendBackResult(Buffer, 0, Buffer.Length);
    }

    /// <summary>
    /// 处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求，返回结果必须加入CommandComposer.AddResponse();
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public override bool ProcessCommand(byte[] buffer, int offset, int count)
    {
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(CommandParser.Command);
        var command = StrToCommand(CommandParser.Command);
        if (!CheckLogin(command)) //检测登录
            return CommandFail(ProtocolCode.UserHasLogined, "");
        try
        {
            return command switch
            {
                Command.Login => DoLogin(),
                Command.Active => DoActive(),
                Command.Message => DoHandleMessage(buffer, offset, count),
                Command.Dir => DoDir(),
                Command.FileList => DoFileList(),
                Command.Download => DoDownload(),
                Command.Upload => DoUpload(),
                Command.SendFile => DoSendFile(),
                Command.Data => DoData(buffer, offset, count),
                _ => throw new ServerProtocolException("Unknow command: " + CommandParser.Command)
            };
        }
        catch (Exception ex)
        {
            return CommandFail(ProtocolCode.ParameterError, ex.Message);
            //ServerInstance.Logger.Error("Unknow command: " + CommandParser.Command);
            //return false;
        }
    }

    /// <summary>
    /// 关键代码
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    private static Command StrToCommand(string command)
    {
        if (compare(ProtocolKey.Active))
            return Command.Active;
        else if (compare(ProtocolKey.Login))
            return Command.Login;
        else if (compare(ProtocolKey.Message))
            return Command.Message;
        else if (compare(ProtocolKey.Dir))
            return Command.Dir;
        else if (compare(ProtocolKey.FileList))
            return Command.FileList;
        else if (compare(ProtocolKey.Download))
            return Command.Download;
        else if (compare(ProtocolKey.Upload))
            return Command.Upload;
        else if (compare(ProtocolKey.SendFile))
            return Command.SendFile;
        else if (compare(ProtocolKey.Data))
            return Command.Data;
        else
            return Command.None;
        bool compare(string key)
        {
            return command.Equals(key, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private bool CheckLogin(Command command)
    {
        if (command is Command.Login || command is Command.Active)
            return true;
        else
            return IsLogin;
    }

    public bool DoSendFile()
    {
        return CommandSucceed([]);
    }

    public bool DoData(byte[] buffer, int offset, int count)
    {
        if (FileStream is null)
            // TODO: ATTENTION logic is not same here
            return CommandFail(ProtocolCode.NotOpenFile, "");
        FileStream.Write(buffer, offset, count);
        ReceviedLength += count;
        if (ReceviedLength == ReceivedFileSize)
        {
            FileStream.Close();
            FileStream.Dispose();
            ReceviedLength = 0;
            IsReceivingFile = false;
#if DEBUG
            // TODO: handle this event
            Server.Tip($"文件接收成功，完成时间{DateTime.Now}", this);
#endif
        }
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(ProtocolKey.Data);
        return CommandSucceed();
    }

    /// <summary>
    /// 处理客户端文件上传
    /// </summary>
    /// <returns></returns>
    public bool DoUpload()
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
        return SendBackResult();
    }

    /// <summary>
    /// 处理客户端文件下载
    /// </summary>
    /// <returns></returns>
    public bool DoDownload()
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
        return SendBackResult();
    }

    private bool DoHandleMessage(byte[] buffer, int offset, int count)
    {
        var message = Encoding.UTF8.GetString(buffer, offset, count);
        UserToken.Server.HandleReceiveMessage(message, this);
        CommandComposer.AddSuccess();
        return SendBackResult();
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
        return SendBackResult();
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
        return SendBackResult();
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
        return SendBackResult();
    }

    
    /// <summary>
    /// 检测文件是否正在使用中，如果正在使用中则检测是否被上传协议占用，如果占用则关闭,真表示正在使用中，并没有关闭
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public bool CheckFileInUse(string filePath)
    {
        if (isFileInUse())
        {
            bool result = true;
            lock (Server.ServerFullHandlerProtocolManager)
            {
                foreach(var fullHandler in Server.ServerFullHandlerProtocolManager)
                {
                    if (!filePath.Equals(fullHandler.FilePath, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                    lock (fullHandler.UserToken) //AsyncSocketUserToken有多个线程访问
                    {
                        fullHandler.UserToken.Close();
                    }
                    result = false;
                }
            }
            return result;
        }
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

    public override void ProcessSend()
    {
        IsSendingAsync = false;
        UserToken.SendBuffer.ClearFirstPacket(); //清除已发送的包
        if (UserToken.SendBuffer.GetFirstPacket(out var offset, out var count))
        {
            IsSendingAsync = true;
            UserToken.SendAsync(offset, count);
        }
        else
            SendCallback();
    }

    /// <summary>
    /// 发送回调函数，用于连续下发数据
    /// </summary>
    /// <returns></returns>
    private void SendCallback()
    {
        if (FileStream is null)
            return;
        if (IsSendingFile) // 发送文件头
        {
            CommandComposer.Clear();
            CommandComposer.AddResponse();
            CommandComposer.AddCommand(ProtocolKey.SendFile);
            _ = CommandSucceed((ProtocolKey.FileSize, FileStream.Length - FileStream.Position));
            IsSendingFile = false;
            return;
        }
        if (IsReceivingFile)
            return;
        // 没有接收文件时
        // 发送具体数据,加FileStream.CanSeek是防止上传文件结束后，文件流被释放而出错
        if (FileStream.CanSeek && FileStream.Position < FileStream.Length)
        {
            CommandComposer.Clear();
            CommandComposer.AddResponse();
            CommandComposer.AddCommand(ProtocolKey.Data);
            ReadBuffer ??= new byte[PacketSize];
            // 避免多次申请内存
            if (ReadBuffer.Length < PacketSize)
                ReadBuffer = new byte[PacketSize];
            var count = FileStream.Read(ReadBuffer, 0, PacketSize);
            _ = CommandSucceed(ReadBuffer, 0, count);
            return;
        }
        // 发送完成
        //ServerInstance.Logger.Info("End Upload file: " + FilePath);
        FileStream.Close();
        FileStream = null;
        FilePath = "";
        IsSendingFile = false;
    }
}
