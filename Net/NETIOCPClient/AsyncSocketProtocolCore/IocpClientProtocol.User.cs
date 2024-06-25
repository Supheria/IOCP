using log4net;

namespace Net;

partial class IocpClientProtocol
{
    // HACK: public static ILog Logger;
    protected string ErrorString { get; set; } = "";

    public UserInfo UserInfo { get; } = new();

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
}
