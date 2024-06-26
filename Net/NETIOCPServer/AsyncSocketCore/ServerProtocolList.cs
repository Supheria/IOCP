using System.Collections;

namespace Net;

public class ServerProtocolList : IList<ServerProtocol>
{
    List<ServerProtocol> List { get; } = [];

    public int Count => List.Count;

    public bool IsReadOnly { get; } = false;

    public ServerProtocol this[int index]
    {
        get => List[index];
        set
        {
            lock (List)
                List[index] = value;
        }
    }

    public void Add(ServerProtocol item)
    {
        lock (List)
            List.Add(item);
    }

    public void Remove(ServerProtocol item)
    {
        lock (List)
            List.Remove(item);
    }

    public void CopyTo(out ServerProtocol[] array)
    {
        lock (List)
        {
            array = new ServerProtocol[List.Count];
            List.CopyTo(array);
        }
    }

    public void CopyTo(ServerProtocol[] array, int arrayIndex)
    {
        lock (List)
            List.CopyTo(array, arrayIndex);
    }

    public void Clear()
    {
        lock (List)
            List.Clear();
    }

    public int IndexOf(ServerProtocol item)
    {
        lock (List)
            return List.IndexOf(item);
    }

    public void Insert(int index, ServerProtocol item)
    {
        lock (List)
            List.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        lock (List)
            List.RemoveAt(index);
    }

    public bool Contains(ServerProtocol item)
    {
        lock (List)
            return List.Contains(item);
    }

    bool ICollection<ServerProtocol>.Remove(ServerProtocol item)
    {
        lock (List)
            return List.Remove(item);
    }

    public IEnumerator<ServerProtocol> GetEnumerator()
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
