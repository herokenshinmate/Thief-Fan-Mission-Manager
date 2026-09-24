using System.Diagnostics;
using System.IO;

namespace ThiefManager.Services;

public class ProcessLauncher : IProcessLauncher
{
    public void Start(string exePath, string? arguments = null)
    {
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(arguments))
            startInfo.Arguments = arguments;

        Process.Start(startInfo);
    }
}
