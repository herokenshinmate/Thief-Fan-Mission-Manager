using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeMissionRepository : IMissionRepository
{
    public List<FanMission> Missions { get; } = new();

    public Task<List<FanMission>> GetAllAsync() => Task.FromResult(Missions.ToList());

    public Task AddAsync(FanMission mission)
    {
        mission.Id = Missions.Count == 0 ? 1 : Missions.Max(m => m.Id) + 1;
        Missions.Add(mission);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FanMission mission)
    {
        var index = Missions.FindIndex(m => m.Id == mission.Id);
        if (index >= 0)
            Missions[index] = mission;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        Missions.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    public Task ApplyThiefGuildMetadataAsync(FanMission fetched)
    {
        var mission = Missions.FirstOrDefault(m => m.Id == fetched.Id);
        if (mission is null)
            return Task.CompletedTask;

        mission.ThiefGuildRating = fetched.ThiefGuildRating;
        mission.ThiefGuildRatingCount = fetched.ThiefGuildRatingCount;
        mission.CampaignMissionCount = fetched.CampaignMissionCount;
        mission.Description = fetched.Description;
        mission.SequelOfTitle = fetched.SequelOfTitle;
        mission.SequelOfUrl = fetched.SequelOfUrl;
        mission.HasSequelTitle = fetched.HasSequelTitle;
        mission.HasSequelUrl = fetched.HasSequelUrl;
        mission.ThiefGuildMetadataVersion = fetched.ThiefGuildMetadataVersion;
        if (string.IsNullOrWhiteSpace(mission.Author))
            mission.Author = fetched.Author;
        if (mission.ReleaseYear is null)
            mission.ReleaseYear = fetched.ReleaseYear;
        if (string.IsNullOrWhiteSpace(mission.Tags))
            mission.Tags = fetched.Tags;
        if (mission.SeriesId is null && !mission.SeriesLookupChecked)
        {
            mission.SeriesId = fetched.SeriesId;
            mission.SeriesPosition = fetched.SeriesPosition;
        }
        mission.SeriesLookupChecked |= fetched.SeriesLookupChecked;
        return Task.CompletedTask;
    }
}
