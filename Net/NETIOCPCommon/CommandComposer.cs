using System.Text;

namespace Net;

public class CommandComposer
{
    List<string> Commands { get; } = [];

    public void Clear()
    {
        Commands.Clear();
    }

    public string GetCommand()
    {
        var str = new StringBuilder();
        if (Commands.Count > 0)
            str.AppendJoin(ProtocolKey.ReturnWrap, Commands);
        return str.ToString();
    }

    public CommandComposer AppendCommand(string commandKey)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Command)
            .Append(ProtocolKey.EqualSign)
            .Append(commandKey)
            .ToString();
        Commands.Add(str);
        return this;
    }

    public CommandComposer AppendSuccess()
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(ProtocolCode.Success)
            .ToString();
        Commands.Add(str);
        return this;
    }

    public CommandComposer AppendFailure(int errorCode, string message)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(errorCode)
            .ToString();
        Commands.Add(str);
        str = new StringBuilder()
            .Append(ProtocolKey.Message)
            .Append(ProtocolKey.EqualSign)
            .Append(message)
            .ToString();
        Commands.Add(str);
        return this;
    }

    public CommandComposer AppendValue(string key, object value)
    {
        var str = new StringBuilder()
            .Append(key)
            .Append(ProtocolKey.EqualSign)
            .Append(value.ToString())
            .ToString();
        Commands.Add(str);
        return this;
    }
}
