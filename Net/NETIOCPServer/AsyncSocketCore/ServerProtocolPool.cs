namespace Net;

public class ServerProtocolPool(int capacity)
{
    Stack<ServerProtocol> Pool { get; } = new(capacity);

    public void Push(ServerProtocol item)
    {
        lock (Pool)
            Pool.Push(item);
    }

    public ServerProtocol Pop()
    {
        lock (Pool)
            return Pool.Pop();
    }

    public int Count => Pool.Count;
}
