using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class FakeProcessLauncher : IProcessLauncher
{
    public string? LastStartedPath { get; private set; }
    public string? LastArguments { get; private set; }
    public void Start(string exePath, string? arguments = null)
    {
        LastStartedPath = exePath;
        LastArguments = arguments;
    }
}

public class ThrowingProcessLauncher : IProcessLauncher
{
    public void Start(string exePath, string? arguments = null) => throw new InvalidOperationException("boom");
}

public class FakeFileExistsChecker : IFileExistsChecker
{
    private readonly HashSet<string> _existingPaths;
    public FakeFileExistsChecker(params string[] existingPaths) => _existingPaths = new HashSet<string>(existingPaths);
    public bool Exists(string path) => _existingPaths.Contains(path);
}

public class LaunchServiceTests
{
    [Fact]
    public void Launch_WithValidConfiguredPath_StartsProcessAndSucceeds()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker(@"C:\Games\Thief2\Thief2.exe"));

        var result = service.Launch(@"C:\Games\Thief2\Thief2.exe");

        Assert.True(result.Ok);
        Assert.Equal(@"C:\Games\Thief2\Thief2.exe", launcher.LastStartedPath);
    }

    [Fact]
    public void Launch_WithNullPath_FailsWithoutStartingProcess()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker());

        var result = service.Launch(null);

        Assert.False(result.Ok);
        Assert.Null(launcher.LastStartedPath);
        Assert.Contains("Settings", result.Error);
    }

    [Fact]
    public void Launch_WithPathThatDoesNotExist_FailsWithoutStartingProcess()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker());

        var result = service.Launch(@"C:\missing.exe");

        Assert.False(result.Ok);
        Assert.Null(launcher.LastStartedPath);
    }

    [Fact]
    public void Launch_WithFmFolderName_PassesFmArgument()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker(@"C:\Games\Thief2\Thief2.exe"));

        var result = service.Launch(@"C:\Games\Thief2\Thief2.exe", "SomeMission");

        Assert.True(result.Ok);
        Assert.Equal("-fm=\"SomeMission\"", launcher.LastArguments);
    }

    [Fact]
    public void Launch_WithoutFmFolderName_PassesNoArguments()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker(@"C:\Games\Thief2\Thief2.exe"));

        var result = service.Launch(@"C:\Games\Thief2\Thief2.exe");

        Assert.True(result.Ok);
        Assert.Null(launcher.LastArguments);
    }

    [Fact]
    public void Launch_WhenProcessStartThrows_FailsWithoutPropagatingException()
    {
        var launcher = new ThrowingProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker(@"C:\Games\Thief2\Thief2.exe"));

        var result = service.Launch(@"C:\Games\Thief2\Thief2.exe");

        Assert.False(result.Ok);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
