//using ClientDemo;

namespace ClientTest
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            //new Client().Show();
            Application.Run(new ClientTestBoostForm());
        }
    }
}
