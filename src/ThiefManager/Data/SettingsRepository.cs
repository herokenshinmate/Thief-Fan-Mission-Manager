using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class SettingsRepository : ISettingsRepository
{
    private const int SingletonId = 1;
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public SettingsRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<AppSettings> GetAsync()
    {
        using var db = _contextFactory();
        var existing = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SingletonId);
        return existing ?? new AppSettings { Id = SingletonId };
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Id = SingletonId;
        using var db = _contextFactory();
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Id == SingletonId);
        if (existing is null)
        {
            db.Settings.Add(settings);
        }
        else
        {
            existing.Thief1FmFolder = settings.Thief1FmFolder;
            existing.Thief2FmFolder = settings.Thief2FmFolder;
            existing.Thief1ExePath = settings.Thief1ExePath;
            existing.Thief2ExePath = settings.Thief2ExePath;
            existing.Thief1DownloadsFolder = settings.Thief1DownloadsFolder;
            existing.Thief2DownloadsFolder = settings.Thief2DownloadsFolder;
        }
        await db.SaveChangesAsync();
    }

    public async Task SetGameCollapsedAsync(GameTitle game, bool collapsed)
    {
        using var db = _contextFactory();
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Id == SingletonId);
        if (existing is null)
        {
            existing = new AppSettings { Id = SingletonId };
            db.Settings.Add(existing);
        }

        if (game == GameTitle.Thief1)
            existing.Thief1Collapsed = collapsed;
        else
            existing.Thief2Collapsed = collapsed;
        await db.SaveChangesAsync();
    }
}
