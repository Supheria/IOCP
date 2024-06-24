using System.Text;

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
        SendCommand(Buffer, 0, Buffer.Length);
    }

    /// <summary>
    /// 处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求，返回结果必须加入CommandComposer.AddResponse();
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    protected override bool ProcessCommand(byte[] buffer, int offset, int count)
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
            UserToken.Server.Tip($"文件接收成功，完成时间{DateTime.Now}", this);
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
            if (UserToken.Server.CheckFileInUse(FilePath))
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
        if (UserToken.Server.CheckFileInUse(FilePath))
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

    private bool DoHandleMessage(byte[] buffer, int offset, int count)
    {
        var message = Encoding.UTF8.GetString(buffer, offset, count);
        UserToken.Server.HandleReceiveMessage(message, this);
        // TODO: for test
#if DEBUG
        SendMessage("result: received");
#endif
        return CommandSucceed();
    }

    // TODO: modify this for common-use
    private bool DoLogin()
    {
        if (!CommandParser.GetValueAsString(ProtocolKey.UserID, out var userID) ||
            !CommandParser.GetValueAsString(ProtocolKey.Password, out var password))
            return CommandFail(ProtocolCode.ParameterError, "");
        var success = userID is "admin" && password is "password";
        if (!success)
        {
            //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userID);
            return CommandFail(ProtocolCode.UserOrPasswordError, "");
        }
        UserID = "admin";
        UserName = "admin";
        IsLogin = true;
        //ServerInstance.Logger.InfoFormat("{0} login success", userID);
        return CommandSucceed(
            (ProtocolKey.UserID, "admin"),
            (ProtocolKey.UserID, "admin")
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

    public override void ProcessSend()
    {
        IsSendingAsync = false;
        UserToken.SendBuffer.ClearFirstPacket(); // 清除已发送的包
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
