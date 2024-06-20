
namespace Net;

public enum IocpProtocolTypes
{
    None = 0,
    SQL = 1, //SQL查询协议
    Upload = 2, //上传协议
    Download = 3, //下载协议
    RemoteStream = 4, //远程文件流协议
    Throughput = 5, //吞吐量测试协议
    Control = 8,
    LogOutput = 9,
    Echart = 10,
    HandlerMessage = 11,//长连接，处理服务端消息
    FullHandler = 12//单Socket长连接，处理所有业务
}
