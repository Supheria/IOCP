using log4net;

namespace Net;

partial class IocpClientProtocol
{
    // HACK: public static ILog Logger;
    protected string ErrorString { get; set; } = "";

    protected string UserID { get; set; } = "";

    protected string UserName { get; set; } = "";

    protected string Password { get; set; } = "";

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
            ErrorString = ProtocolCode.GetErrorCodeString(errorCode);
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
            ErrorString = E.Message;
            //Logger.Error(E.Message);
            return false;
        }
    }

    public bool DoLogin(string userName, string password)
    {
        try
        {
            CommandComposer.Clear();
            CommandComposer.AddRequest();
            CommandComposer.AddCommand(ProtocolKey.Login);
            CommandComposer.AddValue(ProtocolKey.UserName, userName);
            CommandComposer.AddValue(ProtocolKey.Password, BasicFunc.MD5String(password));
            SendCommand();
            return true;
        }
        catch (Exception E)
        {
            //记录日志
            ErrorString = E.Message;
            return false;
        }
    }

    public bool ReConnect()
    {
        if (BasicFunc.SocketConnected(Client.Core) && (Active()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(Client.Core))
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

    public bool ReConnectAndLogin()
    {
        if (BasicFunc.SocketConnected(Client.Core) && (!Active()))
            return true;
        else
        {
            if (!BasicFunc.SocketConnected(Client.Core))
            {
                try
                {
                    Disconnect();
                    Connect(Host, Port);
                    return DoLogin(UserName, Password);
                }
                catch (Exception E)
                {
                    //Logger.Error(E.Message);
                    return false;
                }
            }
            else
                return true;
        }
    }
}
