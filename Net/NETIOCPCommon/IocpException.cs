namespace Net;

public abstract class IocpException(ProtocolCode errorCode, string message) : Exception(message)
{
    public ProtocolCode ErrorCode { get; } = errorCode;

    public override string Message => $"[{ErrorCode}]{base.Message}";
}
