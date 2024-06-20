
using Net.ServerDemo;
//using ServerDemo;
using WarringStates.UI;

namespace ServerTest;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.Run(new ServerForm());
    }
}