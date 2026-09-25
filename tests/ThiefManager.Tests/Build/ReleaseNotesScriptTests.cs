using System.Diagnostics;
using Xunit;

namespace ThiefManager.Tests.Build;

public class ReleaseNotesScriptTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ThiefManager.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static (int ExitCode, string StdErr) RunScript(string version, string outFile)
    {
        var root = RepoRoot();
        var start = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(root, "build", "Get-ReleaseNotes.ps1")}\" -Version {version} -OutFile \"{outFile}\"")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(start)!;
        var stdErr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdErr);
    }

    [Fact]
    public void GetReleaseNotes_ForCurrentVersion_MatchesChangelogData()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"release-notes-{Guid.NewGuid()}.md");
        try
        {
            var (exitCode, stdErr) = RunScript(AppVersion.Current, outFile);

            Assert.True(exitCode == 0, stdErr);
            var expected = ChangelogData.Entries.Single(e => e.Version == AppVersion.Current).Changes.Select(c => "- " + c);
            Assert.Equal(expected, File.ReadAllLines(outFile));
        }
        finally
        {
            File.Delete(outFile);
        }
    }

    [Fact]
    public void GetReleaseNotes_WithMismatchedVersion_Fails()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"release-notes-{Guid.NewGuid()}.md");

        var (exitCode, stdErr) = RunScript("0.0.1", outFile);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("AppVersion.Current", stdErr);
        Assert.False(File.Exists(outFile));
    }
}
