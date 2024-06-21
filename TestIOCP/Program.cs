using ClientTest;
using WarringStates.UI;

namespace TestIOCP
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            new ClientTestBoostForm().Show();
            Application.Run(new ServerForm());
        }
    }
}
