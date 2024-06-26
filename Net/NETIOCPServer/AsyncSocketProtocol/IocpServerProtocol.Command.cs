﻿using System.Text;

namespace Net;

/// <summary>
/// 全功能处理协议
/// </summary>
/// <param name="server"></param>
/// <param name="userToken"></param>
partial class IocpServerProtocol : IDisposable
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

    bool IsLogin { get; set; } = false;

    int PacketSize { get; set; } = 64 * 1024;

    public string FilePath { get; private set; } = "";

    byte[]? ReadBuffer { get; set; } = null;

    FileStream? FileStream { get; set; } = null;

    bool IsSendingFile { get; set; } = false;

    bool IsReceivingFile { get; set; } = false;

    long ReceviedLength { get; set; } = 0;

    long ReceivedFileSize { get; set; } = 0;

    UserInfo UserInfo { get; } = new();

    // TODO: make the dir more common-useable
    public DirectoryInfo RootDirectory { get; set; } = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "upload"));

    string RootDirectoryPath => RootDirectory.FullName;

    public void Dispose()
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
        SendCommand(Buffer, 0, Buffer.Length);
    }

    /// <summary>
    /// 处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求，返回结果必须加入CommandComposer.AddResponse();
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    protected void ProcessCommand(byte[] buffer, int offset, int count)
    {
        CommandComposer.Clear();
        CommandComposer.AddResponse();
        CommandComposer.AddCommand(CommandParser.Command);
        var command = StrToCommand(CommandParser.Command);
        if (!CheckLogin(command)) //检测登录
        {
            _ = CommandFail(ProtocolCode.UserHasLogined, "");
            return;
        }
        switch (command)
        {
            case Command.Login:
                DoLogin();
                return;
            case Command.Active:
                DoActive();
                return;
            case Command.Message:
                DoMessage(buffer, offset, count);
                return;
            case Command.Upload:
                DoUpload();
                return;
            case Command.Download:
                DoDownload();
                return;
            case Command.SendFile:
                DoSendFile();
                return;
            case Command.Data:
                DoData(buffer, offset, count);
                return;
            default:
                return;
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

    public bool DoActive()
    {
        return CommandSucceed();
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
        if (!CommandParser.GetValueAsString(ProtocolKey.DirName, out var dir) ||
            !CommandParser.GetValueAsString(ProtocolKey.FileName, out var filePath) ||
            !CommandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize) /*||*/
            /*!CommandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize)*/)
            return CommandFail(ProtocolCode.ParameterError, "");
        // TODO: modified here for uniform
        dir = dir is "" ? RootDirectoryPath : RootDirectoryPath;
        if (!Directory.Exists(dir))
            return CommandFail(ProtocolCode.DirNotExist, dir);
        FilePath = Path.Combine(dir, filePath);
        FileStream?.Close();
        FileStream = null;
        if (File.Exists(FilePath))
        {
            if (Server.CheckFileInUse(FilePath))
            {
                FilePath = "";
                return CommandFail(ProtocolCode.FileIsInUse, "");
                //ServerInstance.Logger.Error("Start Receive file error, file is in use: " + filePath);
            }
            File.Delete(FilePath);
        }
        FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        IsReceivingFile = true;
        ReceivedFileSize = fileSize;
        return CommandSucceed();
    }

    /// <summary>
    /// 处理客户端文件下载
    /// </summary>
    /// <returns></returns>
    public bool DoDownload()
    {
        if (!CommandParser.GetValueAsString(ProtocolKey.DirName, out var dir) ||
            !CommandParser.GetValueAsString(ProtocolKey.FileName, out var filePath) ||
            !CommandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize) ||
            !CommandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize))
            return CommandFail(ProtocolCode.ParameterError, "");
        dir = dir is "" ? RootDirectoryPath : Path.Combine(RootDirectoryPath, dir);
        if (!Directory.Exists(dir))
            return CommandFail(ProtocolCode.DirNotExist, dir);
        FilePath = Path.Combine(dir, filePath);
        FileStream?.Close(); // 关闭上次传输的文件
        FileStream = null;
        IsSendingFile = false;
        if (!File.Exists(FilePath))
        {
            FilePath = "";
            return CommandFail(ProtocolCode.FileNotExist, "");
        }
        if (Server.CheckFileInUse(FilePath))
        {
            FilePath = "";
            //ServerInstance.Logger.Error("Start download file error, file is in use: " + filePath);
            return CommandFail(ProtocolCode.FileIsInUse, "");
        }
        // 文件以共享只读方式打开，方便多个客户端下载同一个文件。
        FileStream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
        {
            Position = fileSize // 文件移到上次下载位置                        
        };
        IsSendingFile = true;
        PacketSize = packetSize;
        //ServerInstance.Logger.Info("Start download file: " + filePath);
        return CommandSucceed();
    }

    private bool DoMessage(byte[] buffer, int offset, int count)
    {
        var message = Encoding.UTF8.GetString(buffer, offset, count);
        Server.HandleReceiveMessage(message, this);
        // TODO: for test
#if DEBUG
        SendMessage("result: received");
#endif
        return CommandSucceed();
    }

    // TODO: modify this for common-use
    private bool DoLogin()
    {
        if (!CommandParser.GetValueAsString(ProtocolKey.UserID, out var userId) ||
            !CommandParser.GetValueAsString(ProtocolKey.Password, out var password))
            return CommandFail(ProtocolCode.ParameterError, "");
        var success = userId is "admin" && password is "password";
        if (!success)
        {
            //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userID);
            return CommandFail(ProtocolCode.UserOrPasswordError, "");
        }
        UserInfo.Id = userId;
        UserInfo.Name = userId;
        UserInfo.Password = password;
        IsLogin = true;
        //ServerInstance.Logger.InfoFormat("{0} login success", userID);
        return CommandSucceed(
            (ProtocolKey.UserID, userId),
            (ProtocolKey.UserID, userId)
            );
    }

    private bool DoDir()
    {
        if (!CommandParser.GetValueAsString(ProtocolKey.ParentDir, out var dir))
            return CommandFail(ProtocolCode.ParameterError, "");
        if (!Directory.Exists(dir))
            return CommandFail(ProtocolCode.DirNotExist, dir);
        char[] directorySeparator = [Path.DirectorySeparatorChar];
        try
        {
            var values = new List<(string, object)>();
            foreach (var subDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var dirName = subDir.Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries);
                values.Add((ProtocolKey.Item, dirName[dirName.Length - 1]));

            }
            return CommandSucceed(values.ToArray());
        }
        catch (Exception ex)
        {
            return CommandFail(ProtocolCode.UnknowError, ex.Message);
        }
    }

    private bool DoFileList()
    {
        if (!CommandParser.GetValueAsString(ProtocolKey.DirName, out var dir))
            return CommandFail(ProtocolCode.ParameterError, "");
        dir = dir is "" ? RootDirectoryPath : Path.Combine(RootDirectoryPath, dir);
        if (!Directory.Exists(dir))
            return CommandFail(ProtocolCode.DirNotExist, dir);
        try
        {
            var values = new List<(string, object)>();
            foreach (var file in Directory.GetFiles(dir))
            {
                var fileInfo = new FileInfo(file);
                values.Add((ProtocolKey.Item, fileInfo.Name + ProtocolKey.TextSeperator + fileInfo.Length.ToString()));
            }
            return CommandSucceed(values.ToArray());
        }
        catch (Exception ex)
        {
            return CommandFail(ProtocolCode.UnknowError, ex.Message);
        }
    }
}
