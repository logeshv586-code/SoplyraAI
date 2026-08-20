using System.Windows;
using SoplyraAI.Services;

namespace SoplyraAI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(a => a.Equals("--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                SelfTestService.Run();
                Shutdown(0);
            }
            catch
            {
                Shutdown(2);
            }
            return;
        }

        new MainWindow().Show();
    }
}
