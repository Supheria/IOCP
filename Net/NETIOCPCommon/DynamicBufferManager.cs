using System.Text;

namespace Net;

public class DynamicBufferManager(int bufferSize)
{
    byte[] Buffer { get; set; } = new byte[bufferSize];

    public int TotolCount
    {
        get => Buffer.Length;
        set
        {
            try
            {
                var buffer = new byte[value];
                Array.Copy(Buffer, 0, buffer, 0, DataCount);
                Buffer = buffer;
            }
            catch { }
        }
    }

    public int DataCount { get; private set; } = 0;

    public byte[] GetData()
    {
        var data = new byte[DataCount];
        Array.Copy(Buffer, 0, data, 0, data.Length);
        return Buffer;
    }

    public void Clear()
    {
        lock (Buffer)
            DataCount = 0;
    }

    public void RemoveData(int dataCount)
    {
        lock (Buffer)
        {
            if (dataCount >= TotolCount || dataCount >= DataCount)
            {
                DataCount = 0;
                return;
            }
            DataCount -= dataCount;
            for (var i = 0; i < DataCount; i++)
            {
                Buffer[i] = Buffer[dataCount + i];
            }
        }
    }

    public void WriteData(byte[] data)
    {
        WriteData(data, 0, data.Length);
    }

    public void WriteData(byte[] data, int offset, int count)
    {
        lock (Buffer)
        {
            if (data.Length is 0)
                return;
            if (TotolCount - DataCount < count)
            {
                var totalCount = count + DataCount;
                var buffer = new byte[totalCount];
                Array.Copy(Buffer, 0, buffer, 0, DataCount);
                Buffer = buffer;
            }
            Array.Copy(data, offset, Buffer, DataCount, count);
            DataCount += count;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteValue(short value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        var data = BitConverter.GetBytes(value);
        WriteData(data);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteValue(int value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
        {
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        }
        var data = BitConverter.GetBytes(value);
        WriteData(data);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteValue(long value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
        {
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        }
        var data = BitConverter.GetBytes(value);
        WriteData(data);
    }

    /// <summary>
    /// 文本全部转成UTF8，UTF8兼容性好
    /// </summary>
    /// <param name="value"></param>
    public void WriteValue(string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        WriteData(data);
    }
}
