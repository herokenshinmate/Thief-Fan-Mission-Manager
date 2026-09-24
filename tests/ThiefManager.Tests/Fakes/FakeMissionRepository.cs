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

    public Task ApplySeriesLookupAsync(int missionId, int? seriesId, int? seriesPosition)
    {
        var mission = Missions.FirstOrDefault(m => m.Id == missionId);
        if (mission is null)
            return Task.CompletedTask;

        if (mission.SeriesId is null)
        {
            mission.SeriesId = seriesId;
            mission.SeriesPosition = seriesPosition;
        }
        mission.SeriesLookupChecked = true;
        return Task.CompletedTask;
    }
}
