using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class SeriesRepository : ISeriesRepository
{
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public SeriesRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<List<Series>> GetAllAsync()
    {
        using var db = _contextFactory();
        return await db.Series.AsNoTracking().ToListAsync();
    }

    public async Task<Series> GetOrCreateByThiefGuildIdAsync(int thiefGuildSeriesId, string name)
    {
        var trimmedName = name.Trim();
        using var db = _contextFactory();
        var all = await db.Series.ToListAsync();
        var existing = all.FirstOrDefault(s => s.ThiefGuildSeriesId == thiefGuildSeriesId)
            ?? all.FirstOrDefault(s => s.ThiefGuildSeriesId is null
                && string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            // Don't overwrite a name the user may have set; only a newly created series takes
            // the name from Thief Guild.
            existing.ThiefGuildSeriesId = thiefGuildSeriesId;
            await db.SaveChangesAsync();
            return existing;
        }

        var created = new Series { Name = trimmedName, ThiefGuildSeriesId = thiefGuildSeriesId };
        db.Series.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    public async Task<Series> GetOrCreateByNameAsync(string name)
    {
        var trimmedName = name.Trim();
        using var db = _contextFactory();
        // Loaded into memory so the comparison is a true case-insensitive match (SQLite's
        // lower() only folds ASCII); the table holds at most a few dozen rows.
        var existing = (await db.Series.ToListAsync())
            .FirstOrDefault(s => string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;

        var created = new Series { Name = trimmedName };
        db.Series.Add(created);
        await db.SaveChangesAsync();
        return created;
    }

    public async Task RenameAsync(int id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        using var db = _contextFactory();
        var entity = await db.Series.FindAsync(id);
        if (entity is null)
            return;

        entity.Name = name.Trim();
        await db.SaveChangesAsync();
    }

    public async Task SetAllExpandedAsync(bool isExpanded)
    {
        using var db = _contextFactory();
        foreach (var series in await db.Series.ToListAsync())
            series.IsExpanded = isExpanded;
        await db.SaveChangesAsync();
    }

    public async Task SetExpandedAsync(int id, bool isExpanded)
    {
        using var db = _contextFactory();
        var entity = await db.Series.FindAsync(id);
        if (entity is null)
            return;

        entity.IsExpanded = isExpanded;
        await db.SaveChangesAsync();
    }

    public async Task<List<SeriesPart>> GetAllPartsAsync()
    {
        using var db = _contextFactory();
        return await db.SeriesParts.AsNoTracking().ToListAsync();
    }

    public async Task ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)
    {
        using var db = _contextFactory();
        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => p.SeriesId == seriesId).ToListAsync());
        db.SeriesParts.AddRange(parts.Select(p => new SeriesPart
        {
            SeriesId = seriesId,
            Position = p.Position,
            Title = p.Title,
            ThiefGuildUrl = p.ThiefGuildUrl
        }));
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _contextFactory();
        foreach (var mission in await db.FanMissions.Where(m => m.SeriesId == id).ToListAsync())
        {
            mission.SeriesId = null;
            mission.SeriesPosition = null;
            mission.SeriesLookupChecked = true;
        }

        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => p.SeriesId == id).ToListAsync());

        var entity = await db.Series.FindAsync(id);
        if (entity is not null)
            db.Series.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteOrphansAsync()
    {
        using var db = _contextFactory();
        var usedIds = await db.FanMissions
            .Where(m => m.SeriesId != null)
            .Select(m => m.SeriesId!.Value)
            .Distinct()
            .ToListAsync();
        var orphans = await db.Series.Where(s => !usedIds.Contains(s.Id)).ToListAsync();
        if (orphans.Count == 0)
            return;

        var orphanIds = orphans.Select(s => s.Id).ToList();
        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => orphanIds.Contains(p.SeriesId)).ToListAsync());
        db.Series.RemoveRange(orphans);
        await db.SaveChangesAsync();
    }
}
