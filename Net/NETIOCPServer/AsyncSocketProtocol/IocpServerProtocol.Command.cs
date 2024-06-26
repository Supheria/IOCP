﻿using System.Text;

namespace Net;

/// <summary>
/// 全功能处理协议
/// </summary>
/// <param name="server"></param>
/// <param name="userToken"></param>
partial class ServerProtocol : IocpProtocol
{
    int PacketSize { get; set; } = 64 * 1024;

    bool IsSendingFile { get; set; } = false;

    bool IsReceivingFile { get; set; } = false;

    long ReceviedLength { get; set; } = 0;

    long ReceivedFileSize { get; set; } = 0;

    // TODO: make the dir more common-useable
    public DirectoryInfo RootDirectory { get; set; } = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "upload"));

    string RootDirectoryPath => RootDirectory.FullName;

    /// <summary>
    /// 发送消息到客户端，由消息来驱动业务逻辑，接收方必须返回应答，否则认为发送不成功
    /// </summary>
    /// <param name="message">消息</param>
    public void SendMessage(string message)
    {
        var commandComposer = new CommandComposer();
        commandComposer.AppendCommand(ProtocolKey.Message);
        commandComposer.AppendSuccess();
        var buffer = Encoding.UTF8.GetBytes(message);
        SendCommand(commandComposer, buffer, 0, buffer.Length);
    }

    /// <summary>
    /// 处理分完包的数据，子类从这个方法继承,服务端在此处处理所有的客户端命令请求
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    protected override void ProcessCommand(CommandParser commandParser, byte[] buffer, int offset, int count)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.Command, out var command))
            return;
        if (!CheckLogin(command)) //检测登录
        {
            CommandFail(ProtocolCode.UserHasLogined, "");
            return;
        }
        switch (command)
        {
            case ProtocolKey.Login:
                DoLogin(commandParser);
                return;
            case ProtocolKey.Active:
                DoActive();
                return;
            case ProtocolKey.Message:
                DoMessage(buffer, offset, count);
                return;
            case ProtocolKey.Upload:
                DoUpload(commandParser);
                return;
            case ProtocolKey.Download:
                DoDownload(commandParser);
                return;
            case ProtocolKey.SendFile:
                DoSendFile();
                return;
            case ProtocolKey.Data:
                DoData(buffer, offset, count);
                return;
            default:
                return;
        }
    }
    protected void CommandFail(int errorCode, string message)
    {
        var commandComposer = new CommandComposer()
            .AppendFailure(errorCode, message);
        SendCommand(commandComposer);
    }

    protected void CommandSucceed(CommandComposer commandComposer)
    {
        CommandSucceed(commandComposer, [], 0, 0);
    }

    protected void CommandSucceed(CommandComposer commandComposer, byte[] buffer, int offset, int count)
    {
        commandComposer.AppendSuccess();
        SendCommand(commandComposer, buffer, offset, count);
    }

    private bool CheckLogin(string command)
    {
        if (command is ProtocolKey.Login || command is ProtocolKey.Active)
            return true;
        else
            return IsLogin;
    }

    private void DoActive()
    {
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Active);
        CommandSucceed(commandComposer);
    }

    private void DoSendFile()
    {
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.SendFile);
        CommandSucceed(commandComposer);
    }

    public void DoData(byte[] buffer, int offset, int count)
    {
        if (FileStream is null)
        {
            CommandFail(ProtocolCode.NotOpenFile, "");
            return;
        }
        FileStream.Write(buffer, offset, count);
        ReceviedLength += count;
        if (ReceviedLength == ReceivedFileSize)
        {
            FileStream.Close();
            FileStream.Dispose();
            ReceviedLength = 0;
            IsReceivingFile = false;
#if DEBUG
            Server.Tip($"文件接收成功，完成时间{DateTime.Now}", this);
#endif
        }
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Data);
        CommandSucceed(commandComposer);
    }

    /// <summary>
    /// 处理客户端文件上传
    /// </summary>
    /// <returns></returns>
    public void DoUpload(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.DirName, out var dir) ||
            !commandParser.GetValueAsString(ProtocolKey.FileName, out var filePath) ||
            !commandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize) /*||*/
            /*!CommandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize)*/)
        {
            CommandFail(ProtocolCode.ParameterError, "");
            return;
        }
        // TODO: modified here for uniform
        dir = dir is "" ? RootDirectoryPath : RootDirectoryPath;
        if (!Directory.Exists(dir))
        {
            CommandFail(ProtocolCode.DirNotExist, dir);
            return;
        }
        FilePath = Path.Combine(dir, filePath);
        FileStream?.Close();
        FileStream = null;
        if (File.Exists(FilePath))
        {
            if (Server.CheckFileInUse(FilePath))
            {
                FilePath = "";
                CommandFail(ProtocolCode.FileIsInUse, "");
                return;
                //ServerInstance.Logger.Error("Start Receive file error, file is in use: " + filePath);
            }
            File.Delete(FilePath);
        }
        FileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        IsReceivingFile = true;
        ReceivedFileSize = fileSize;
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Upload);
        CommandSucceed(commandComposer);
    }

    /// <summary>
    /// 处理客户端文件下载
    /// </summary>
    /// <returns></returns>
    public void DoDownload(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.DirName, out var dir) ||
            !commandParser.GetValueAsString(ProtocolKey.FileName, out var filePath) ||
            !commandParser.GetValueAsLong(ProtocolKey.FileSize, out var fileSize) ||
            !commandParser.GetValueAsInt(ProtocolKey.PacketSize, out var packetSize))
        {
            CommandFail(ProtocolCode.ParameterError, "");
            return;
        }
        dir = dir is "" ? RootDirectoryPath : Path.Combine(RootDirectoryPath, dir);
        if (!Directory.Exists(dir))
        {
            CommandFail(ProtocolCode.DirNotExist, dir);
            return;
        }
        FilePath = Path.Combine(dir, filePath);
        FileStream?.Close(); // 关闭上次传输的文件
        FileStream = null;
        IsSendingFile = false;
        if (!File.Exists(FilePath))
        {
            FilePath = "";
            CommandFail(ProtocolCode.FileNotExist, "");
            return;
        }
        if (Server.CheckFileInUse(FilePath))
        {
            FilePath = "";
            //ServerInstance.Logger.Error("Start download file error, file is in use: " + filePath);
            CommandFail(ProtocolCode.FileIsInUse, "");
            return;
        }
        // 文件以共享只读方式打开，方便多个客户端下载同一个文件。
        FileStream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
        {
            Position = fileSize // 文件移到上次下载位置                        
        };
        IsSendingFile = true;
        PacketSize = packetSize;
        //ServerInstance.Logger.Info("Start download file: " + filePath);
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Download);
        CommandSucceed(commandComposer);
    }

    private void DoMessage(byte[] buffer, int offset, int count)
    {
        var message = Encoding.UTF8.GetString(buffer, offset, count);
        Server.HandleReceiveMessage(message, this);
        // TODO: for test
#if DEBUG
        SendMessage("result: received");
#endif
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Message);
        CommandSucceed(commandComposer);
    }

    // TODO: modify this for common-use
    private void DoLogin(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.UserID, out var userId) ||
            !commandParser.GetValueAsString(ProtocolKey.Password, out var password))
        {
            CommandFail(ProtocolCode.ParameterError, "");
            return;
        }
        var success = userId is "admin" && password is "password";
        if (!success)
        {
            //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userID);
            CommandFail(ProtocolCode.UserOrPasswordError, "");
            return;
        }
        UserInfo.Id = userId;
        UserInfo.Name = userId;
        UserInfo.Password = password;
        IsLogin = true;
        //ServerInstance.Logger.InfoFormat("{0} login success", userID);
        var commandComposer = new CommandComposer()
            .AppendCommand(ProtocolKey.Login)
            .AppendValue(ProtocolKey.UserID, UserInfo.Id)
            .AppendValue(ProtocolKey.UserName, UserInfo.Name);
        CommandSucceed(commandComposer);
    }

    private void DoDir(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.ParentDir, out var dir))
        {
            CommandFail(ProtocolCode.ParameterError, "");
            return;
        }
        if (!Directory.Exists(dir))
        {
            CommandFail(ProtocolCode.DirNotExist, dir);
            return;
        }
        char[] directorySeparator = [Path.DirectorySeparatorChar];
        try
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Dir);
            foreach (var subDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                var dirName = subDir.Split(directorySeparator, StringSplitOptions.RemoveEmptyEntries);
                commandComposer.AppendValue(ProtocolKey.Item, dirName[dirName.Length - 1]);

            }
            CommandSucceed(commandComposer);
        }
        catch (Exception ex)
        {
            CommandFail(ProtocolCode.UnknowError, ex.Message);
        }
    }

    private void DoFileList(CommandParser commandParser)
    {
        if (!commandParser.GetValueAsString(ProtocolKey.DirName, out var dir))
        {
            CommandFail(ProtocolCode.ParameterError, "");
            return;
        }
        dir = dir is "" ? RootDirectoryPath : Path.Combine(RootDirectoryPath, dir);
        if (!Directory.Exists(dir))
        {
            CommandFail(ProtocolCode.DirNotExist, dir);
            return;
        }
        try
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.FileList);
            foreach (var file in Directory.GetFiles(dir))
            {
                var fileInfo = new FileInfo(file);
                commandComposer.AppendValue(ProtocolKey.Item, fileInfo.Name + ProtocolKey.TextSeperator + fileInfo.Length.ToString());
            }
            CommandSucceed(commandComposer);
        }
        catch (Exception ex)
        {
            CommandFail(ProtocolCode.UnknowError, ex.Message);
        }
    }
}
