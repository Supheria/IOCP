using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using Net;

namespace NETIOCPClient.AsyncSocketCore
{
    public class AsynchronousClient
    {        
        // ManualResetEvent instances signal completion.  
        public static ManualResetEvent connectDone = new ManualResetEvent(false);
        public static ManualResetEvent sendDone = new ManualResetEvent(false); 
        protected DynamicBufferManager m_recvBuffer; //接收数据的缓存
        public Socket client { get; set; }
        
        public int SendTimeout
        {
            get { return client.SendTimeout; }
            set { client.SendTimeout = value; }
        }
        public int ReceiveTimeout
        {
            get { return client.ReceiveTimeout; }
            set { client.ReceiveTimeout = value; }
        }
        public AsynchronousClient()
        {
            // Create a TCP/IP socket.  
            client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);            
            m_recvBuffer = new DynamicBufferManager(ConstTabel.ReceiveBufferSize);                     
        }
        public bool Connect(string host,int port)
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
                connectDone.Reset();
                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);                
                connectDone.WaitOne();
                result = client.Connected;//是否准确？首次使用是准确的，往后使用可能不准确 
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());                
            }
            return result;
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
            connectDone.Set();
        }
        public void Send(Socket client, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            sendDone.Reset();
            try
            {
                client.BeginSend(buffer, offset, size, SocketFlags.None, new AsyncCallback(SendCallback), client);
            }
            catch (Exception ex)
            {                
                NETIOCPClient.AsyncSocketProtocolCore.AsyncClientBaseSocket.Logger.Error("AsynchronousClient.cs Send(Socket client, byte[] buffer, int offset, int size, SocketFlags socketFlags) Exception:" + ex.Message);                
                //throw ex;//抛出异常并重置异常的抛出点，异常堆栈中前面的异常被丢失
                throw;//抛出异常，但不重置异常抛出点，异常堆栈中的异常不会丢失
            }
            sendDone.WaitOne();
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
            sendDone.Set();
        }
    }
}
