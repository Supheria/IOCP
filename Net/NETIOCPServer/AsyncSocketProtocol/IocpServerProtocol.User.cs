﻿using Net;
using System;

namespace Net;

partial class IocpServerProtocol
{
    public string UserID { get; protected set; } = "";

    public string UserName { get; protected set; } = "";

    public string UserPermissions { get; protected set; } = "";

    public bool IsLogin { get; protected set; } = false;

    public string SocketFlag { get; protected set; } = "";

    // TODO: refine below

    public bool DoLogin()
    {
        if (CommandParser.GetValueAsString(ProtocolKey.UserName, out var userName) & CommandParser.GetValueAsString(ProtocolKey.Password, out var password))
        {
            if (password.Equals(BasicFunc.MD5String("admin"), StringComparison.CurrentCultureIgnoreCase))
            {
                CommandComposer.AddSuccess();
                UserName = userName;
                IsLogin = true;
                //ServerInstance.Logger.InfoFormat("{0} login success", userName);
            }
            else
            {
                CommandComposer.AddFailure(ProtocolCode.UserOrPasswordError, "");
                //ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userName);
            }
        }
        else
            CommandComposer.AddFailure(ProtocolCode.ParameterError, "");
        return SendBackResult();
    }

    public bool DoActive()
    {
        CommandComposer.AddSuccess();
        return SendBackResult();
    }
}
