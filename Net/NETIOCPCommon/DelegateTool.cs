using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net;

public delegate void IocpEventHandler(IocpProtocol protocol);

public delegate void IocpEventHandler<TArgs>(IocpProtocol iocpProtocol, TArgs args);

public static class DelegateTool
{
    public static void InvokeAsync(this IocpEventHandler onEvent, IocpProtocol protocol)
    {
        new Task(() => onEvent?.Invoke(protocol)).Start();
    }

    public static void InvokeAsync<TArgs>(this IocpEventHandler<TArgs> onEvent, IocpProtocol protocol, TArgs args)
    {
        new Task(() => onEvent?.Invoke(protocol, args)).Start();
    }

    public static void InvokeAsync(this EventHandler onEvent, object? obj, EventArgs args)
    {
        new Task(() => onEvent?.Invoke(obj, args)).Start();
    }

    public static void InvokeAsync<TArgs>(this EventHandler<TArgs> onEvent, object obj, TArgs args)
    {
        new Task(() => onEvent?.Invoke(obj, args)).Start();
    }
}
