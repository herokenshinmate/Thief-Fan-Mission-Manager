using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeSettingsRepository : ISettingsRepository
{
    private AppSettings _settings = new() { Id = 1 };

    public Task<AppSettings> GetAsync() => Task.FromResult(new AppSettings
    {
        Id = _settings.Id,
        Thief1FmFolder = _settings.Thief1FmFolder,
        Thief2FmFolder = _settings.Thief2FmFolder,
        Thief1ExePath = _settings.Thief1ExePath,
        Thief2ExePath = _settings.Thief2ExePath
    });

    public Task SaveAsync(AppSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
