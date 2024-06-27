using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Net;

public partial class ServerProtocol(IocpServer server)
{
    IocpServer Server { get; } = server;

    object AcceptLocker { get; } = new();

    public bool ProcessAccept(Socket? acceptSocket)
    {
        lock (AcceptLocker)
        {
            if (acceptSocket is null || Socket is not null)
                return false;
            Socket = acceptSocket;
            // 设置TCP Keep-alive数据包的发送间隔为10秒
            Socket.IOControl(IOControlCode.KeepAliveValues, KeepAlive(1, 1000 * 10, 1000 * 10), null);
            SocketInfo.Connect(acceptSocket);
            return true;
        }
    }

    /// <summary>
    /// keep alive 设置
    /// </summary>
    /// <param name="onOff">是否开启（1为开，0为关）</param>
    /// <param name="keepAliveTime">当开启keep-alive后，经过多长时间（ms）开启侦测</param>
    /// <param name="keepAliveInterval">多长时间侦测一次（ms）</param>
    /// <returns>keep alive 输入参数</returns>
    private static byte[] KeepAlive(int onOff, int keepAliveTime, int keepAliveInterval)
    {
        byte[] buffer = new byte[12];
        BitConverter.GetBytes(onOff).CopyTo(buffer, 0);
        BitConverter.GetBytes(keepAliveTime).CopyTo(buffer, 4);
        BitConverter.GetBytes(keepAliveInterval).CopyTo(buffer, 8);
        return buffer;
    }

    /// <summary>
    /// 发送回调函数，用于连续下发数据
    /// </summary>
    /// <returns></returns>
    protected override void SendCallback()
    {
        if (FileStream is null)
            return;
        if (IsSendingFile) // 发送文件头
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.SendFile)
                .AppendValue(ProtocolKey.FileLength, FileStream.Length - FileStream.Position)
                .AppendSuccess();
            SendCommand(commandComposer);
            IsSendingFile = false;
            return;
        }
        if (IsReceivingFile)
            return;
        // 没有接收文件时
        // 发送具体数据,加FileStream.CanSeek是防止上传文件结束后，文件流被释放而出错
        if (FileStream.CanSeek && FileStream.Position < FileStream.Length)
        {
            var commandComposer = new CommandComposer()
                .AppendCommand(ProtocolKey.Data)
                .AppendSuccess();
            ReadBuffer ??= new byte[PacketSize];
            // 避免多次申请内存
            if (ReadBuffer.Length < PacketSize)
                ReadBuffer = new byte[PacketSize];
            var count = FileStream.Read(ReadBuffer, 0, PacketSize);
            SendCommand(commandComposer, ReadBuffer, 0, count);
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
