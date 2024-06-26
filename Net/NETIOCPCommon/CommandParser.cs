using System.Diagnostics.CodeAnalysis;

namespace Net;

/// <summary>
/// 收到数据的解析器，用于解析返回的内容
/// </summary>
public class CommandParser
{
    public string Header { get; private set; } = "";

    public string Command { get; private set; } = "";

    public List<string> Names { get; } = [];

    public List<string> Values { get; } = [];

    public bool DecodeProtocolText(string protocolText)
    {
        Header = "";
        Names.Clear();
        Values.Clear();
        if (protocolText.IndexOf(ProtocolKey.ReturnWrap) < 0)
            return false;
        var nameValues = protocolText.Split([ProtocolKey.ReturnWrap], StringSplitOptions.RemoveEmptyEntries);
        if (nameValues.Length < 2) // 每次命令至少包括两行
            return false;
        for (int i = 0; i < nameValues.Length; i++)
        {
            var str = nameValues[i].Split([ProtocolKey.EqualSign], StringSplitOptions.None);
            if (str.Length < 2) // 不存在等号
                continue;
            if (str.Length > 2) //超过两个等号，返回失败
                return false;
            if (str[0].Equals(ProtocolKey.Command, StringComparison.CurrentCultureIgnoreCase))
            {
                Command = str[1];
            }
            else
            {
                Names.Add(str[0].ToLower());
                Values.Add(str[1]);
            }
        }
        return true;
    }

    public bool GetValueAsString(string protocolKey, [NotNullWhen(true)] out string? value)
    {
        value = null;
        var index = Names.IndexOf(protocolKey.ToLower());
        if (index > -1)
        {
            value = Values[index];
            return true;
        }
        return false;
    }

    public List<string> GetValue(string protocolKey)
    {
        var result = new List<string>();
        for (int i = 0; i < Names.Count; i++)
        {
            if (protocolKey.Equals(Names[i], StringComparison.CurrentCultureIgnoreCase))
                result.Add(Values[i]);
        }
        return result;
    }

    public bool GetValueAsShort(string protocolKey, out short value)
    {
        value = 0;
        var index = Names.IndexOf(protocolKey.ToLower());
        return index > -1 && short.TryParse(Values[index], out value);
    }

    public bool GetValueAsInt(string protocolKey, out int value)
    {
        value = 0;
        var index = Names.IndexOf(protocolKey.ToLower());
        return index > -1 && int.TryParse(Values[index], out value);
    }

    public bool GetValueAsLong(string protocolKey, out long value)
    {
        value = 0;
        var index = Names.IndexOf(protocolKey.ToLower());
        return index > -1 && long.TryParse(Values[index], out value);
    }

    public bool GetValueAsFloat(string protocolKey, out float value)
    {
        value = 0f;
        var index = Names.IndexOf(protocolKey.ToLower());
        return index > -1 && float.TryParse(Values[index], out value);
    }

    public bool GetValueAsDouble(string protocolKey, out double value)
    {
        value = 0d;
        var index = Names.IndexOf(protocolKey.ToLower());
        return index > -1 && double.TryParse(Values[index], out value);
    }
}
