using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Net;

public class IocpClient
{
    // ManualResetEvent instances signal completion.  
    public static ManualResetEvent ConnectDone { get; } = new(false);

    public static ManualResetEvent SendDone { get; } = new(false);

    /// <summary>
    /// Create a TCP/IP socket.
    /// </summary>
    public Socket Core { get; set; } = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    
    public int TimeoutMilliseconds
    {
        get => Core.ReceiveTimeout;
        set
        {
            Core.ReceiveTimeout = value;
            Core.SendTimeout = value;       
        }
    }

    public delegate void HandleMessage(string message);

    public event HandleMessage? OnReceiveMessage;

    public void HandleReceiveMessage(string message)
    {
        new Task(() => OnReceiveMessage?.Invoke(message)).Start();
    }

    public delegate void HandleProcess();

    public event HandleProcess? OnDownload;

    public void HandleDownload()
    {
        new Task(() => OnDownload?.Invoke()).Start();
    }

    public event HandleProcess? OnUpload;

    public void HandleUpload()
    {
        new Task(() => OnUpload?.Invoke()).Start();
    }

    public bool Connect(string host, int port)
    {
        bool result = false;
        // Connect to a remote device.  
        try
        {
            IPAddress ipAddress;
            if (Regex.Matches(host, "[a-zA-Z]").Count > 0)//支持域名解析
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(host);
                ipAddress = ipHostInfo.AddressList[0];
            }
            else
            {
                ipAddress = IPAddress.Parse(host);
            }
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

            // Connect to the remote endpoint.  
            ConnectDone.Reset();
            Core.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Core);
            ConnectDone.WaitOne();
            result = Core.Connected;//是否准确？首次使用是准确的，往后使用可能不准确 
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        return result;
    }
    public void Disconnect()
    {
        Core.Close();
    }

    private void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete the connection.  
            client.EndConnect(ar);
#if DEBUG
            Console.WriteLine("Socket connected to {0}",
                client.RemoteEndPoint.ToString());
#endif                     
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
        // Signal that the connection has been made.  
        ConnectDone.Set();
    }
    public void Send(Socket client, byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        SendDone.Reset();
        try
        {
            client.BeginSend(buffer, offset, size, SocketFlags.None, new AsyncCallback(SendCallback), client);
        }
        catch (Exception ex)
        {
            //Net.IocpClientProtocol.Logger.Error("AsynchronousClient.cs Send(Socket client, byte[] buffer, int offset, int size, SocketFlags socketFlags) Exception:" + ex.Message);
            //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
            throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
        }
        SendDone.WaitOne();
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket client = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSend = client.EndSend(ar);
            //#if DEBUG
            //                Console.WriteLine("Send {0} bytes to server.", bytesSend);
            //#endif
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e.ToString());
#endif
        }
        // Signal that all bytes have been sent.  
        SendDone.Set();
    }
}
