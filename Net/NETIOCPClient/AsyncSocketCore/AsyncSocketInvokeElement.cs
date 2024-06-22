using System.Net.Sockets;
using System.Text;

namespace Net
{
    public class AsyncSocketInvokeElement
    {
        protected AsynchronousClient m_tcpClient;
        protected string m_host;
        protected int m_port;
        protected IocpProtocolTypes m_protocolFlag;
        protected int SocketTimeOutMS { get { return m_tcpClient.SendTimeout; } set { m_tcpClient.SendTimeout = value; m_tcpClient.ReceiveTimeout = value; } }
        private bool m_netByteOrder;
        public bool NetByteOrder { get { return m_netByteOrder; } set { m_netByteOrder = value; } } //长度是否使用网络字节顺序
        protected CommandComposer m_outgoingDataAssembler; //协议组装器，用来组装往外发送的命令
        protected DynamicBufferManager m_recvBuffer; //接收数据的缓存
        protected CommandParser m_incomingDataParser; //收到数据的解析器，用于解析返回的内容
        protected DynamicBufferManager m_sendBuffer; //发送数据的缓存，统一写到内存中，调用一次发送  

        public AsyncSocketInvokeElement()
        {
            m_tcpClient = new AsynchronousClient();
            m_protocolFlag = IocpProtocolTypes.None;
            SocketTimeOutMS = ConstTabel.TimeoutMilliseconds;
            m_outgoingDataAssembler = new CommandComposer();
            m_recvBuffer = new DynamicBufferManager(ConstTabel.ReceiveBufferSize);
            m_incomingDataParser = new CommandParser();
            m_sendBuffer = new DynamicBufferManager(ConstTabel.ReceiveBufferSize);
        }
        /// <summary>
        /// 设置SOCKET是否延迟发送
        /// </summary>
        /// <param name="NoDelay"></param>
        public void SetNoDelay(bool NoDelay)
        {
            m_tcpClient.client.NoDelay = NoDelay;
        }
        public void Connect(string host, int port)
        {
            try
            {
                if (m_tcpClient.Connect(host, port))
                {
                    byte[] socketFlag = new byte[1];
                    socketFlag[0] = (byte)m_protocolFlag;
                    m_tcpClient.Send(m_tcpClient.client, socketFlag, 0, 1, SocketFlags.None); //发送标识            
                    m_host = host;
                    m_port = port;
                }
                else
                    throw new System.Exception("Connection failed");
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine(ex.Message);
            }
        }
        public void Disconnect()
        {
            m_tcpClient.client.Close();
            m_tcpClient = new AsynchronousClient();
        }
        public void SendCommand()
        {
            string commandText = m_outgoingDataAssembler.GetProtocolText();
            byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
            int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length; //获取总大小
            m_sendBuffer.Clear();
            m_sendBuffer.WriteInt(totalLength, false); //写入总大小
            m_sendBuffer.WriteInt(bufferUTF8.Length, false); //写入命令大小
            m_sendBuffer.WriteBuffer(bufferUTF8); //写入命令内容
            m_tcpClient.Send(m_tcpClient.client, m_sendBuffer.Buffer, 0, m_sendBuffer.DataCount, SocketFlags.None);
        }
        public void SendCommand(byte[] buffer, int offset, int count)
        {
            string commandText = m_outgoingDataAssembler.GetProtocolText();
            byte[] bufferUTF8 = Encoding.UTF8.GetBytes(commandText);
            int totalLength = sizeof(int) + sizeof(int) + bufferUTF8.Length + count; //获取总大小
            m_sendBuffer.Clear();
            m_sendBuffer.WriteInt(totalLength, false); //写入总大小
            m_sendBuffer.WriteInt(bufferUTF8.Length, false); //写入命令大小
            m_sendBuffer.WriteBuffer(bufferUTF8); //写入命令内容
            m_sendBuffer.WriteBuffer(buffer, offset, count); //写入二进制数据
            m_tcpClient.Send(m_tcpClient.client, m_sendBuffer.Buffer, 0, m_sendBuffer.DataCount, SocketFlags.None);
        }
    }
}
