using System.Collections;

namespace Net;

public class ServerProtocolList : IList<IocpServerProtocol>
{
    List<IocpServerProtocol> List { get; } = [];

    public int Count => List.Count;

    public bool IsReadOnly { get; } = false;

    public IocpServerProtocol this[int index]
    {
        get => List[index];
        set
        {
            lock (List)
                List[index] = value;
        }
    }

    public void Add(IocpServerProtocol item)
    {
        lock (List)
            List.Add(item);
    }

    public void Remove(IocpServerProtocol item)
    {
        lock (List)
            List.Remove(item);
    }

    public void CopyTo(out IocpServerProtocol[] array)
    {
        lock (List)
        {
            array = new IocpServerProtocol[List.Count];
            List.CopyTo(array);
        }
    }

    public void CopyTo(IocpServerProtocol[] array, int arrayIndex)
    {
        lock (List)
            List.CopyTo(array, arrayIndex);
    }

    public void Clear()
    {
        lock (List)
            List.Clear();
    }

    public int IndexOf(IocpServerProtocol item)
    {
        lock (List)
            return List.IndexOf(item);
    }

    public void Insert(int index, IocpServerProtocol item)
    {
        lock (List)
            List.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        lock (List)
            List.RemoveAt(index);
    }

    public bool Contains(IocpServerProtocol item)
    {
        lock (List)
            return List.Contains(item);
    }

    bool ICollection<IocpServerProtocol>.Remove(IocpServerProtocol item)
    {
        lock (List)
            return List.Remove(item);
    }

    public IEnumerator<IocpServerProtocol> GetEnumerator()
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
