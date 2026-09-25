using Velopack;

namespace ThiefManager;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before any WPF startup: it handles Velopack's install, update and uninstall
        // hooks, some of which exit the process immediately. SetAutoApplyOnStartup(false) stops
        // Velopack from silently applying a downloaded update here: updates only ever apply when
        // the user clicks Restart to update, and a pending update is simply offered again (via
        // the startup check) on the next launch.
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
