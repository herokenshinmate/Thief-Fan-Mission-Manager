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

    public async Task ApplyThiefGuildMetadataAsync(FanMission fetched)
    {
        using var db = _contextFactory();
        var entity = await db.FanMissions.FindAsync(fetched.Id);
        if (entity is null)
            return;

        entity.ThiefGuildRating = fetched.ThiefGuildRating;
        entity.ThiefGuildRatingCount = fetched.ThiefGuildRatingCount;
        entity.CampaignMissionCount = fetched.CampaignMissionCount;
        entity.Description = fetched.Description;
        entity.SequelOfTitle = fetched.SequelOfTitle;
        entity.SequelOfUrl = fetched.SequelOfUrl;
        entity.HasSequelTitle = fetched.HasSequelTitle;
        entity.HasSequelUrl = fetched.HasSequelUrl;
        entity.ThiefGuildMetadataVersion = fetched.ThiefGuildMetadataVersion;
        if (string.IsNullOrWhiteSpace(entity.Author))
            entity.Author = fetched.Author;
        if (entity.ReleaseYear is null)
            entity.ReleaseYear = fetched.ReleaseYear;
        if (string.IsNullOrWhiteSpace(entity.Tags))
            entity.Tags = fetched.Tags;
        if (entity.SeriesId is null && !entity.SeriesLookupChecked)
        {
            entity.SeriesId = fetched.SeriesId;
            entity.SeriesPosition = fetched.SeriesPosition;
        }
        entity.SeriesLookupChecked |= fetched.SeriesLookupChecked;
        await db.SaveChangesAsync();
    }
}
