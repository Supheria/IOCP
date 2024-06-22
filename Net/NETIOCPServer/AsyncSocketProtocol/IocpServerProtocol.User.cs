using Net;
using System;

namespace Net;

partial class IocpServerProtocol
{
    public string UserID { get; protected set; } = "";

    public string UserName { get; protected set; } = "";

    public string UserPermissions { get; protected set; } = "";

    public bool IsLogin { get; protected set; } = false;

    public string SocketFlag { get; protected set; } = "";

    //public virtual bool DoLogin()
    //{
    //    if (!CommandParser.GetValueAsString(ProtocolKey.UserName, out var userName) ||
    //        !CommandParser.GetValueAsString(ProtocolKey.Password, out var password))
    //        return CommandFail(ProtocolCode.ParameterError, "");
    //    var success = password.Equals(BasicFunc.MD5String("admin"), StringComparison.CurrentCultureIgnoreCase);
    //    if (!success)
    //    {
    //        return CommandFail(ProtocolCode.UserOrPasswordError, "");
    //        //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userName);
    //    }
    //    UserName = userName;
    //    IsLogin = true;
    //    //ServerInstance.Logger.InfoFormat("{0} login success", userName);
    //    return CommandSucceed();
    //}

    public bool DoActive()
    {
        return CommandSucceed();
    }
}
