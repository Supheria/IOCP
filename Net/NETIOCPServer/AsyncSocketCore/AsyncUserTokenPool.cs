using System;
using System.Collections.Generic;

namespace Net;

public class AsyncUserTokenPool(int capacity)
{
    Stack<AsyncUserToken> Pool { get; } = new(capacity);

    public void Push(AsyncUserToken item)
    {
        lock (Pool)
            Pool.Push(item);
    }

    public AsyncUserToken Pop()
    {
        lock (Pool)
            return Pool.Pop();
    }

    public int Count => Pool.Count;
}
