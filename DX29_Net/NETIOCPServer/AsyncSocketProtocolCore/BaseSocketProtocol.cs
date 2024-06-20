using Net;
using System;

namespace Net
{
    public class BaseSocketProtocol : AsyncSocketInvokeElement
    {
        protected string m_userName;
        public string UserName { get { return m_userName; } }
        protected string m_userPermissions;
        public string UserPermissions { get { return m_userPermissions; } }
        protected string m_userID;
        public string UserID { get { return m_userID; } }
        protected bool m_logined;
        public bool Logined { get { return m_logined; } }
        protected string m_socketFlag;
        public string SocketFlag { get { return m_socketFlag; } }

        public BaseSocketProtocol(AsyncSocketServer asyncSocketServer, AsyncSocketUserToken asyncSocketUserToken)
            : base(asyncSocketServer, asyncSocketUserToken)
        {
            m_userID = "";
            m_userName = "";
            m_userPermissions = "";
            m_logined = false;
            m_socketFlag = "";
        }

        public bool DoLogin()
        {
            string userName = "";
            string password = "";
            if (m_incomingDataParser.GetValue(ProtocolKey.UserName, ref userName) & m_incomingDataParser.GetValue(ProtocolKey.Password, ref password))
            {
                if (password.Equals(BasicFunc.MD5String("admin"), StringComparison.CurrentCultureIgnoreCase))
                {
                    m_outgoingDataAssembler.AddSuccess();
                    m_userName = userName;
                    m_logined = true;
                    ServerInstance.Logger.InfoFormat("{0} login success", userName);
                }
                else
                {
                    m_outgoingDataAssembler.AddFailure(ProtocolCode.UserOrPasswordError, "");
                    ServerInstance.Logger.ErrorFormat("{0} login failure,password error", userName);
                }
            }
            else
                m_outgoingDataAssembler.AddFailure(ProtocolCode.ParameterError, "");
            return DoSendResult();
        }

        public bool DoActive()
        {
            m_outgoingDataAssembler.AddSuccess();
            return DoSendResult();
        }
    }
}
