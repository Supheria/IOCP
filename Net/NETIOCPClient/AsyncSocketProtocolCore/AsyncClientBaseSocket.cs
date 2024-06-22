using log4net;

namespace Net;

public class AsyncClientBaseSocket : AsyncSocketInvokeElement
{
    public static ILog Logger;
    protected string m_errorString;
    public string ErrorString { get { return m_errorString; } }
    protected string m_userID;
    protected string m_userName;
    protected string m_password;
    public AsyncClientBaseSocket()
        : base()
    {
        DateTime currentTime = DateTime.Now;
        log4net.GlobalContext.Properties["LogDir"] = currentTime.ToString("yyyyMM");
        log4net.GlobalContext.Properties["LogFileName"] = "_SocketAsyncServer" + currentTime.ToString("yyyyMMdd");
        Logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }

    public bool CheckErrorCode()
    {
        m_incomingDataParser.GetValueAsInt(ProtocolKey.Code, out var errorCode);
        if (errorCode == ProtocolCode.Success)
            return true;
        else
        {
            m_errorString = ProtocolCode.GetErrorCodeString(errorCode);
            return false;
        }
    }

    public bool DoActive()
    {
        try
        {
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddRequest();
            m_outgoingDataAssembler.AddCommand(ProtocolKey.Active);
            SendCommand();
            return true;
        }
        catch (Exception E)
        {
            //记录日志
            m_errorString = E.Message;
            Logger.Error(E.Message);
            return false;
        }
    }

    public bool DoLogin(string userName, string password)
    {
        try
        {
            m_outgoingDataAssembler.Clear();
            m_outgoingDataAssembler.AddRequest();
            m_outgoingDataAssembler.AddCommand(ProtocolKey.Login);
            m_outgoingDataAssembler.AddValue(ProtocolKey.UserName, userName);
            m_outgoingDataAssembler.AddValue(ProtocolKey.Password, BasicFunc.MD5String(password));
            SendCommand();
            return true;
        }
        catch (Exception E)
        {
            //记录日志
            m_errorString = E.Message;
            return false;
        }
    }

    public bool ReConnect()
    {
        if (BasicFunc.SocketConnected(m_tcpClient.client) && (DoActive()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(m_tcpClient.client))
            {
                try
                {
                    Connect(m_host, m_port);
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

    public bool ReConnectAndLogin()
    {
        if (BasicFunc.SocketConnected(m_tcpClient.client) && (!DoActive()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(m_tcpClient.client))
            {
                try
                {
                    Disconnect();
                    Connect(m_host, m_port);
                    return DoLogin(m_userName, m_password);
                }
                catch (Exception E)
                {
                    Logger.Error(E.Message);
                    return false;
                }
            }
            else
                return true;
        }
    }
}
