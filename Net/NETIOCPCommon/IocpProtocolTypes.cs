
namespace Net;

public enum IocpProtocolTypes
{
    None = 0,
    /// <summary>
    /// SQL查询协议
    /// </summary>
    SQL = 1,
    /// <summary>
    /// 上传协议
    /// </summary>
    Upload = 2,
    /// <summary>
    /// 下载协议
    /// </summary>
    Download = 3,
    /// <summary>
    /// 远程文件流协议
    /// </summary>
    RemoteStream = 4,
    /// <summary>
    /// 吞吐量测试协议
    /// </summary>
    Throughput = 5,
    Control = 8,
    LogOutput = 9,
    Echart = 10,
    /// <summary>
    /// 长连接，处理服务端消息
    /// </summary>
    HandlerMessage = 11,
    /// <summary>
    /// 单Socket长连接，处理所有业务
    /// </summary>
    FullHandler = 12
}
