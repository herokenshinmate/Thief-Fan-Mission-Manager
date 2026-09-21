namespace ThiefManager.Services;

public class LaunchService
{
    private readonly IProcessLauncher _launcher;
    private readonly IFileExistsChecker _fileExistsChecker;

    public LaunchService(IProcessLauncher launcher, IFileExistsChecker fileExistsChecker)
    {
        _launcher = launcher;
        _fileExistsChecker = fileExistsChecker;
    }

    public LaunchResult Launch(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !_fileExistsChecker.Exists(exePath))
            return LaunchResult.Failure("No valid executable is configured for this game. Set it in Settings.");

        _launcher.Start(exePath);
        return LaunchResult.Success();
    }
}
