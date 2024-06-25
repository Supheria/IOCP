using log4net;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Net;

partial class IocpClientProtocol
{
    // HACK: public static ILog Logger;

    public static ManualResetEvent ConnectDone { get; } = new(false);

    public static ManualResetEvent SendDone { get; } = new(false);

    SocketAsyncEventArgs ReceiveAsyncArgs { get; } = new();

    SocketAsyncEventArgs SendAsyncArgs { get; } = new();

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

    public IocpClientProtocol()
    {

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
            Host = host;
            Port = port;
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

    //public IocpClientProtocol()
    //    : base()
    //{
    //    DateTime currentTime = DateTime.Now;
    //    log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
    //    log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
    //    Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    //}

    public bool CheckErrorCode()
    {
        CommandParser.GetValueAsInt(ProtocolKey.Code, out var errorCode);
        if (errorCode == ProtocolCode.Success)
            return true;
        else
        {
            //ErrorString = ProtocolCode.GetErrorCodeString(errorCode);
            return false;
        }
    }

    public bool Active()
    {
        try
        {
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Active);
            SendCommand();
            return true;
        }
        catch (Exception E)
        {
            //记录日志
            //ErrorString = E.Message;
            //Logger.Error(E.Message);
            return false;
        }
    }

    public bool ReConnect()
    {
        if (BasicFunc.SocketConnected(Core) && (Active()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(Core))
            {
                try
                {
                    Connect(Host, Port);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
                return true;
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
}
