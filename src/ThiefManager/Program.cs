using Velopack;

namespace ThiefManager;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before any WPF startup: it handles Velopack's install, update and uninstall
        // hooks, some of which exit the process immediately.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
