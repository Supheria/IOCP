using System.Collections;

namespace Net;

public class ServerFullHandlerProtocolManager : IEnumerable<ServerFullHandlerProtocol>
{
    List<ServerFullHandlerProtocol> List { get; } = [];

    public int Count()
    {
        return List.Count;
    }

    public ServerFullHandlerProtocol ElementAt(int index)
    {
        return List.ElementAt(index);
    }

    public void Add(ServerFullHandlerProtocol value)
    {
        List.Add(value);
    }

    public void Remove(ServerFullHandlerProtocol value)
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
            ((ServerFullHandlerProtocol)item).SendMessage(msg);
        }
    }

    public IEnumerator<ServerFullHandlerProtocol> GetEnumerator()
    {
        return List.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return List.GetEnumerator();
    }
}
