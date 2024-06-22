using System.Text;

namespace Net;

public class DynamicBufferManager(int bufferSize)
{
    /// <summary>
    /// 存放内存的数组
    /// </summary>
    public byte[] Buffer { get; set; } = new byte[bufferSize];

    /// <summary>
    /// 写入数据大小
    /// </summary>
    public int DataCount { get; set; } = 0;

    public int BufferSize => Buffer.Length;

    /// <summary>
    /// 获得当前写入的字节数
    /// </summary>
    /// <returns></returns>
    public int GetDataCount()
    {
        return DataCount;
    }

    /// <summary>
    /// 获得剩余的字节数
    /// </summary>
    /// <returns></returns>
    public int GetReserveCount()
    {
        return Buffer.Length - DataCount;
    }

    public void Clear()
    {
        DataCount = 0;
    }

    /// <summary>
    /// 清理指定大小的数据
    /// </summary>
    /// <param name="count"></param>
    public void Clear(int count)
    {
        // 如果需要清理的数据大于现有数据大小，则全部清理
        if (count >= DataCount)
        {
            DataCount = 0;
            return;
        }
        // 否则后面的数据往前移
        for (var i = 0; i < DataCount - count; i++)
        {
            Buffer[i] = Buffer[count + i];
        }
        DataCount -= count;
    }

    /// <summary>
    /// 设置缓存大小
    /// </summary>
    /// <param name="size"></param>
    public void SetBufferSize(int size)
    {
        if (Buffer.Length < size)
        {
            var temp = new byte[size];
            Array.Copy(Buffer, 0, temp, 0, DataCount); // 复制以前的数据
            Buffer = temp; // 替换
        }
    }

    public void WriteBuffer(byte[] buffer, int offset, int count)
    {
        if (buffer.Length is 0)
            return;
        // 缓冲区空间够，不需要申请
        if (GetReserveCount() >= count)
        {
            Array.Copy(buffer, offset, Buffer, DataCount, count);
            DataCount += count;
            return;
        }
        // 缓冲区空间不够，需要申请更大的内存，并进行移位
        var totalSize = Buffer.Length + count - GetReserveCount(); //总大小-空余大小
        var temp = new byte[totalSize];
        Array.Copy(Buffer, 0, temp, 0, DataCount); //复制以前的数据
        Array.Copy(buffer, offset, temp, DataCount, count); //复制新写入的数据
        DataCount += count;
        Buffer = temp; //替换
    }

    public void WriteBuffer(byte[] buffer)
    {
        WriteBuffer(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteShort(short value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
        {
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        }
        byte[] tmpBuffer = BitConverter.GetBytes(value);
        WriteBuffer(tmpBuffer);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteInt(int value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
        {
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        }
        byte[] tmpBuffer = BitConverter.GetBytes(value);
        WriteBuffer(tmpBuffer);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="hostToNetworkOrder">NET是小头结构，网络字节是大头结构，需要客户端和服务器约定好</param>
    public void WriteLong(long value, bool hostToNetworkOrder)
    {
        if (hostToNetworkOrder)
        {
            value = System.Net.IPAddress.HostToNetworkOrder(value);
        }
        byte[] tmpBuffer = BitConverter.GetBytes(value);
        WriteBuffer(tmpBuffer);
    }

    /// <summary>
    /// 文本全部转成UTF8，UTF8兼容性好
    /// </summary>
    /// <param name="value"></param>
    public void WriteString(string value)
    {
        byte[] tmpBuffer = Encoding.UTF8.GetBytes(value);
        WriteBuffer(tmpBuffer);
    }
}
