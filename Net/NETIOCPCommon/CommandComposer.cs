using System.Text;

namespace Net;

public class CommandComposer
{
    StringBuilder Commands { get; } = new();

    public void Clear()
    {
        Commands.Clear();
    }

    public string GetCommand()
    {
        return Commands.ToString();
    }

    public CommandComposer AppendCommand(string commandKey)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Command)
            .Append(ProtocolKey.EqualSign)
            .Append(commandKey)
            .ToString();
        Commands.Append(str)
            .Append(ProtocolKey.ReturnWrap);
        return this;
    }

    public CommandComposer AppendSuccess()
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(ProtocolCode.Success)
            .ToString();
        Commands.Append(str)
            .Append(ProtocolKey.ReturnWrap);
        return this;
    }

    public CommandComposer AppendFailure(int errorCode, string message)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(errorCode)
            .ToString();
        Commands.Append(str)
            .Append(ProtocolKey.ReturnWrap);
        str = new StringBuilder()
            .Append(ProtocolKey.Message)
            .Append(ProtocolKey.EqualSign)
            .Append(message)
            .ToString();
        Commands.Append(str)
            .Append(ProtocolKey.ReturnWrap);
        return this;
    }

    public CommandComposer AppendValue(string key, object value)
    {
        var str = new StringBuilder()
            .Append(key)
            .Append(ProtocolKey.EqualSign)
            .Append(value.ToString())
            .ToString();
        Commands.Append(str)
            .Append(ProtocolKey.ReturnWrap);
        return this;
    }
}
