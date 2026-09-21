using System.Diagnostics;

namespace ThiefManager.Services;

public class ProcessLauncher : IProcessLauncher
{
    public void Start(string exePath) =>
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
}
