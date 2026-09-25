using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeSettingsRepository : ISettingsRepository
{
    private readonly AppSettings _settings = new() { Id = 1 };

    public Task<AppSettings> GetAsync() => Task.FromResult(new AppSettings
    {
        Id = _settings.Id,
        Thief1FmFolder = _settings.Thief1FmFolder,
        Thief2FmFolder = _settings.Thief2FmFolder,
        Thief1ExePath = _settings.Thief1ExePath,
        Thief2ExePath = _settings.Thief2ExePath,
        Thief1DownloadsFolder = _settings.Thief1DownloadsFolder,
        Thief2DownloadsFolder = _settings.Thief2DownloadsFolder,
        Thief1Collapsed = _settings.Thief1Collapsed,
        Thief2Collapsed = _settings.Thief2Collapsed
    });

    public Task SaveAsync(AppSettings settings)
    {
        _settings.Thief1FmFolder = settings.Thief1FmFolder;
        _settings.Thief2FmFolder = settings.Thief2FmFolder;
        _settings.Thief1ExePath = settings.Thief1ExePath;
        _settings.Thief2ExePath = settings.Thief2ExePath;
        _settings.Thief1DownloadsFolder = settings.Thief1DownloadsFolder;
        _settings.Thief2DownloadsFolder = settings.Thief2DownloadsFolder;
        return Task.CompletedTask;
    }

    public Task SetGameCollapsedAsync(GameTitle game, bool collapsed)
    {
        if (game == GameTitle.Thief1)
            _settings.Thief1Collapsed = collapsed;
        else
            _settings.Thief2Collapsed = collapsed;
        return Task.CompletedTask;
    }
}
