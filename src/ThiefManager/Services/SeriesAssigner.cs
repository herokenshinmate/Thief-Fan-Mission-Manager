using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Services;

public static class SeriesAssigner
{
    /// <summary>
    /// Applies a Thief Guild lookup's series info to a mission in memory (the caller persists it).
    /// A mission already in a series keeps it, since that may have been set by hand. Either way
    /// the mission is marked as checked so the startup backfill doesn't fetch it again.
    /// </summary>
    public static async Task ApplyAsync(FanMission mission, ThiefGuildSeriesInfo? info, ISeriesRepository seriesRepository)
    {
        mission.SeriesLookupChecked = true;
        if (mission.SeriesId is not null || info is null)
            return;

        var series = await seriesRepository.GetOrCreateByThiefGuildIdAsync(info.ThiefGuildSeriesId, info.Name);
        mission.SeriesId = series.Id;
        mission.SeriesPosition = info.Position;
    }
}
