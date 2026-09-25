using System.Diagnostics;
using System.Text;
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

    private static (int ExitCode, string StdErr) RunScript(string version, string outFile, string? repoRoot = null)
    {
        var root = RepoRoot();
        var repoRootArg = repoRoot is null ? string.Empty : $" -RepoRoot \"{repoRoot}\"";
        var start = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(root, "build", "Get-ReleaseNotes.ps1")}\" -Version {version} -OutFile \"{outFile}\"{repoRootArg}")
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

        try
        {
            var (exitCode, stdErr) = RunScript("0.0.1", outFile);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("AppVersion.Current", stdErr);
            Assert.False(File.Exists(outFile));
        }
        finally
        {
            if (File.Exists(outFile))
                File.Delete(outFile);
        }
    }

    [Fact]
    public void GetReleaseNotes_WithNonAsciiChangelog_KeepsCharactersIntact()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"release-notes-fixture-{Guid.NewGuid()}");
        var outFile = Path.Combine(tempDir, "release-notes.md");
        try
        {
            var srcDir = Path.Combine(tempDir, "src", "ThiefManager");
            Directory.CreateDirectory(srcDir);

            var utf8NoBom = new UTF8Encoding(false);
            File.WriteAllText(
                Path.Combine(srcDir, "AppVersion.cs"),
                "public const string Current = \"9.9.9\";",
                utf8NoBom);
            File.WriteAllText(
                Path.Combine(srcDir, "ChangelogEntry.cs"),
                "new(\"9.9.9\", \"2026-01-01\", new[] { \"Banner — shows ★ stats · \\\"quoted\\\"\" }),",
                utf8NoBom);

            var (exitCode, stdErr) = RunScript("9.9.9", outFile, tempDir);

            Assert.True(exitCode == 0, stdErr);
            var lines = File.ReadAllLines(outFile, Encoding.UTF8);
            Assert.Equal(["- Banner — shows ★ stats · \"quoted\""], lines);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
