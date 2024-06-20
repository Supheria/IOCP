using ClientDemo;

namespace ClientTest
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.Run(new Client());
        }
    }
}
