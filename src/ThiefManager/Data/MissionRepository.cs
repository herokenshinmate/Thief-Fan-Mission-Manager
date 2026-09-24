using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class MissionRepository : IMissionRepository
{
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public MissionRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<List<FanMission>> GetAllAsync()
    {
        using var db = _contextFactory();
        return await db.FanMissions.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(FanMission mission)
    {
        using var db = _contextFactory();
        db.FanMissions.Add(mission);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FanMission mission)
    {
        using var db = _contextFactory();
        db.FanMissions.Update(mission);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _contextFactory();
        var entity = await db.FanMissions.FindAsync(id);
        if (entity is null)
            return;

        db.FanMissions.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task ApplySeriesLookupAsync(int missionId, int? seriesId, int? seriesPosition)
    {
        using var db = _contextFactory();
        var entity = await db.FanMissions.FindAsync(missionId);
        if (entity is null)
            return;

        if (entity.SeriesId is null)
        {
            entity.SeriesId = seriesId;
            entity.SeriesPosition = seriesPosition;
        }
        entity.SeriesLookupChecked = true;
        await db.SaveChangesAsync();
    }
}
