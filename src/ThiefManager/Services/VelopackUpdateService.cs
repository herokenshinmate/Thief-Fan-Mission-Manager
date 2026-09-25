using Velopack;
using Velopack.Sources;

namespace ThiefManager.Services;

/// <summary>Updates from this repo's public GitHub Releases, as published by .github/workflows/release.yml.</summary>
public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/herokenshinmate/ThiefFMManager";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private UpdateInfo? _downloaded;

    public bool IsInstalled => _manager.IsInstalled;

    public async Task<AvailableUpdate?> CheckAndDownloadAsync()
    {
        // Velopack's DownloadUpdatesAsync takes a global lock; without this, a manual check
        // started while the startup check is still downloading can fail instead of just waiting.
        await _checkLock.WaitAsync();
        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info is null)
                return null;

            await _manager.DownloadUpdatesAsync(info);
            _downloaded = info;
            return new AvailableUpdate(info.TargetFullRelease.Version.ToString(), info.TargetFullRelease.NotesMarkdown);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    public void ApplyAndRestart()
    {
        if (_downloaded is not null)
            _manager.ApplyUpdatesAndRestart(_downloaded);
    }
}
