using System.Collections;

namespace Net;

public class ServerFullHandlerProtocolManager : IEnumerable<IocpServerProtocol>
{
    List<IocpServerProtocol> List { get; } = [];

    public int Count()
    {
        return List.Count;
    }

    public IocpServerProtocol ElementAt(int index)
    {
        return List.ElementAt(index);
    }

    public void Add(IocpServerProtocol value)
    {
        List.Add(value);
    }

    public void Remove(IocpServerProtocol value)
    {
        List.Remove(value);
    }
    /// <summary>
    /// 向在线的客户端广播
    /// </summary>
    /// <param name="msg">广播信息</param>
    public void Broadcast(string msg)
    {
        foreach (var item in List)
        {
            ((IocpServerProtocol)item).SendMessage(msg);
        }
    }

    public IEnumerator<IocpServerProtocol> GetEnumerator()
    {
        return List.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return List.GetEnumerator();
    }
}
