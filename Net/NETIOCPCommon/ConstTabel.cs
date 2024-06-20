
namespace Net;

public class ConstTabel
{
    public static int InitBufferSize = 1024 * 4; //解析命令初始缓存大小        
    public static int ReceiveBufferSize = 1024 * 4; //IOCP接收数据缓存大小，设置过小会造成事件响应增多，设置过大会造成内存占用偏多
    public static int SocketTimeOutMS = 60 * 1000; //Socket超时设置为60秒    补充一点，接收BuffSize >= 发送BuffSize >= 实际发送Size，对于内外部的Buffer都适用
}
