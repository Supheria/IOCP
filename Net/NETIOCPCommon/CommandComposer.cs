using System;
using System.Collections.Generic;
using System.Text;

namespace Net;

public class CommandComposer
{
    List<string> ProtocolText { get; } = [];

    public void Clear()
    {
        ProtocolText.Clear();
    }

    public string GetProtocolText()
    {
        var str = new StringBuilder();
        if (ProtocolText.Count > 0)
            str.AppendJoin(ProtocolKey.ReturnWrap, ProtocolText);
        return str.ToString();
    }

    public void AddRequest()
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.LeftBrackets)
            .Append(ProtocolKey.Request)
            .Append(ProtocolKey.RightBrackets)
            .ToString();
        ProtocolText.Add(str);
    }

    public void AddResponse()
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.LeftBrackets)
            .Append(ProtocolKey.Response)
            .Append(ProtocolKey.RightBrackets)
            .ToString();
        ProtocolText.Add(str);
    }

    public void AddCommand(string commandKey)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Command)
            .Append(ProtocolKey.EqualSign)
            .Append(commandKey)
            .ToString();
        ProtocolText.Add(str);
    }

    public void AddSuccess()
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(ProtocolCode.Success)
            .ToString();
        ProtocolText.Add(str);
    }

    public void AddFailure(int errorCode, string message)
    {
        var str = new StringBuilder()
            .Append(ProtocolKey.Code)
            .Append(ProtocolKey.EqualSign)
            .Append(errorCode)
            .ToString();
        ProtocolText.Add(str);
        str = new StringBuilder()
            .Append(ProtocolKey.Message)
            .Append(ProtocolKey.EqualSign)
            .Append(message)
            .ToString();
        ProtocolText.Add(str);
    }

    public void AddValue(string protocolKey, object value)
    {
        var str = new StringBuilder()
            .Append(protocolKey)
            .Append(ProtocolKey.EqualSign)
            .Append(value.ToString())
            .ToString();
        ProtocolText.Add(str);
    }
}
