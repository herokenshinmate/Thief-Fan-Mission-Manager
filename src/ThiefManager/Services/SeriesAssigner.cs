using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Services;

public static class SeriesAssigner
{
    /// <summary>
    /// Applies a Thief Guild lookup's series info to a mission in memory (the caller persists the
    /// mission). A series is only assigned automatically the first time a mission is looked up:
    /// once SeriesLookupChecked is set — by an earlier lookup or by ungrouping — the mission's
    /// series is the user's to change. The series' complete part list is refreshed whenever the
    /// mission belongs to that same Thief Guild series.
    /// </summary>
    public static async Task ApplyAsync(FanMission mission, ThiefGuildSeriesInfo? info, ISeriesRepository seriesRepository)
    {
        if (info is not null && mission.SeriesId is null && !mission.SeriesLookupChecked)
        {
            var series = await seriesRepository.GetOrCreateByThiefGuildIdAsync(info.ThiefGuildSeriesId, info.Name);
            mission.SeriesId = series.Id;
            mission.SeriesPosition = info.Position;
        }
        mission.SeriesLookupChecked = true;

        if (info is not null && mission.SeriesId is int seriesId)
            await ReplacePartsIfSameSeriesAsync(seriesId, info, seriesRepository);
    }

    /// <summary>
    /// Stores <paramref name="info"/>'s part list for the series, but only when that series is the
    /// same Thief Guild series the info came from and the info actually lists parts.
    /// </summary>
    public static async Task ReplacePartsIfSameSeriesAsync(int seriesId, ThiefGuildSeriesInfo info, ISeriesRepository seriesRepository)
    {
        if (info.Parts is not { Count: > 0 } parts)
            return;

        var series = (await seriesRepository.GetAllAsync()).FirstOrDefault(s => s.Id == seriesId);
        if (series?.ThiefGuildSeriesId != info.ThiefGuildSeriesId)
            return;

        await seriesRepository.ReplacePartsAsync(seriesId, parts
            .Select(p => new SeriesPart { SeriesId = seriesId, Position = p.Position, Title = p.Title, ThiefGuildUrl = p.Url })
            .ToList());
    }
}
