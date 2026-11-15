using System.Linq;
using System.Threading;
using System.Windows;
namespace TetherGuard;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!e.Args.Contains("--smoke-test"))
        {
            instance = new Mutex(true, @"Local\TetherGuard.Desktop", out bool created);
            if (!created)
            {
                MessageBox.Show("TetherGuard is already running. Open it from the system tray.", "TetherGuard");
                Shutdown();
                return;
            }
        }
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
