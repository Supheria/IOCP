namespace Net;

public class ConstTabel
{
    /// <summary>
    /// 解析命令初始缓存大小
    /// </summary>
    public static int InitBufferSize { get; } = 1024 * 4;

    /// <summary>
    /// IOCP接收数据缓存大小，设置过小会造成事件响应增多，设置过大会造成内存占用偏多
    /// </summary>
    public static int ReceiveBufferSize { get; } = 1024 * 4;


    public static int TransferBufferMax { get; } = 1024 * 1024;

    /// <summary>
    /// Socket超时设置为60秒
    /// </summary>
    public static int TimeoutMilliseconds { get; } = 60 * 1000;
}
