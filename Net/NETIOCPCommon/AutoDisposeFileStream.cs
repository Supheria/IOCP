namespace Net;

public class AutoDisposeFileStream
{
    public string TimeStamp { get; }

    FileStream FileStream { get; set; }

    System.Timers.Timer Timer { get; } = new();

    public bool IsExpired { get; private set; } = false;

    public long Length => FileStream.Length;

    public long Position
    {
        get => FileStream.Position;
        set => FileStream.Position = value;
    }

    public delegate void HandleEvent(AutoDisposeFileStream autoFile);

    public event HandleEvent? OnClosed;

    public AutoDisposeFileStream(string timeStamp, FileStream fileStream, int expireMilliseconds)
    {
        TimeStamp = timeStamp;
        FileStream = fileStream;
        Timer.Interval = expireMilliseconds;
        Timer.Elapsed += (_, _) => Close();
        Timer.Start();
    }

    public void Close()
    {
        FileStream.Dispose();
        Timer.Dispose();
        IsExpired = true;
        OnClosed?.Invoke(this);
    }

    public bool Read(byte[] buffer, int offset, int count, out int readCount)
    {
        readCount = 0;
        if (IsExpired)
            return false;
        Timer.Stop();
        readCount = FileStream.Read(buffer, offset, count);
        Timer.Start();
        return true;
    }

    public bool Write(byte[] buffer, int offset, int count)
    {
        if (IsExpired)
            return false;
        Timer.Stop();
        FileStream.Write(buffer, offset, count);
        Timer.Start();
        return true;
    }
}
