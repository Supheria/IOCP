using System.Collections;

namespace Net;

public class AsyncUserTokenList : IList<AsyncUserToken>
{
    List<AsyncUserToken> List { get; } = [];

    public int Count => List.Count;

    public bool IsReadOnly { get; } = false;

    public AsyncUserToken this[int index]
    {
        get => List[index];
        set
        {
            lock (List)
                List[index] = value;
        }
    }

    public void Add(AsyncUserToken item)
    {
        lock (List)
            List.Add(item);
    }

    public void Remove(AsyncUserToken item)
    {
        lock (List)
            List.Remove(item);
    }

    public void CopyTo(out AsyncUserToken[] array)
    {
        lock (List)
        {
            array = new AsyncUserToken[List.Count];
            List.CopyTo(array);
        }
    }

    public void CopyTo(AsyncUserToken[] array, int arrayIndex)
    {
        lock (List)
            List.CopyTo(array, arrayIndex);
    }

    public void Clear()
    {
        lock (List)
            List.Clear();
    }

    public int IndexOf(AsyncUserToken item)
    {
        lock (List)
            return List.IndexOf(item);
    }

    public void Insert(int index, AsyncUserToken item)
    {
        lock (List)
            List.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        lock (List)
            List.RemoveAt(index);
    }

    public bool Contains(AsyncUserToken item)
    {
        lock (List)
            return List.Contains(item);
    }

    bool ICollection<AsyncUserToken>.Remove(AsyncUserToken item)
    {
        lock (List)
            return List.Remove(item);
    }

    public IEnumerator<AsyncUserToken> GetEnumerator()
    {
        lock (List)
            return List.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        lock (List)
            return List.GetEnumerator();
    }
}
