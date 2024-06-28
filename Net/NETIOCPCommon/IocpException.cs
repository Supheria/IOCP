namespace Net;

public class IocpException(ProtocolCode errorCode, string message) : Exception(message)
{
    public ProtocolCode ErrorCode { get; } = errorCode;

    public override string Message => $"[{ErrorCode}]{base.Message}";

    public IocpException(ProtocolCode errorCode) : this(errorCode, "")
    {

    }
}
