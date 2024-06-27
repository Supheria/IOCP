namespace Net;

public class ServerProtocolException(ProtocolCode errorCode, string message) : IocpException(errorCode, message)
{
    public ServerProtocolException(ProtocolCode errorCode) : this(errorCode, "")
    {

    }

}
