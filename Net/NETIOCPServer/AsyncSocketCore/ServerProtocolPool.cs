namespace Net;

public class ServerProtocolPool(int capacity)
{
    Stack<IocpServerProtocol> Pool { get; } = new(capacity);

    public void Push(IocpServerProtocol item)
    {
        lock (Pool)
            Pool.Push(item);
    }

    public IocpServerProtocol Pop()
    {
        lock (Pool)
            return Pool.Pop();
    }

    public int Count => Pool.Count;
}
