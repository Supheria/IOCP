namespace Net;

public class ClientProtocolException(ProtocolCode errorCode, string message) : IocpException(errorCode, message)
{
    public ClientProtocolException(ProtocolCode errorCode) : this(errorCode, "")
    {

    }
}
