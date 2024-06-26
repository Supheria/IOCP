using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Net;

public class CommandParser
{
    Dictionary<string, string> Map { get; } = [];

    public static CommandParser Parse(string command)
    {
        var result = new CommandParser();
        var lines = command.Split([ProtocolKey.ReturnWrap], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var pair = line.Split([ProtocolKey.EqualSign], StringSplitOptions.None);
            if (pair.Length < 2)
                continue;
            result.Map[pair[0]] = pair[1];
        }
        return result;
    }

    public bool GetValueAsString(string key, [NotNullWhen(true)] out string? value)
    {
        return Map.TryGetValue(key, out value);
    }

    public bool GetValueAsShort(string key, out short value)
    {
        Map.TryGetValue(key, out var str);
        return short.TryParse(str, out value);
    }

    public bool GetValueAsInt(string key, out int value)
    {
        Map.TryGetValue(key, out var str);
        return int.TryParse(str, out value);
    }

    public bool GetValueAsLong(string key, out long value)
    {
        Map.TryGetValue(key, out var str);
        return long.TryParse(str, out value);
    }

    public bool GetValueAsFloat(string key, out float value)
    {
        Map.TryGetValue(key, out var str);
        return float.TryParse(str, out value);
    }

    public bool GetValueAsDouble(string key, out double value)
    {
        Map.TryGetValue(key, out var str);
        return double.TryParse(str, out value);
    }
}
