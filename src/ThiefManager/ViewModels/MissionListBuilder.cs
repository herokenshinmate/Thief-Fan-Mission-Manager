using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public static class MissionListBuilder
{
    /// <summary>
    /// Turns MissionQuery's filtered, sorted missions into list rows. Each series becomes one unit
    /// placed where its first member appears in the sorted input, which puts it among missions with
    /// similar values for every sort field. The exception is Title, where the unit is ordered by the
    /// series name. Members follow their header in series order regardless of the chosen sort, and
    /// only when the series is expanded.
    /// </summary>
    public static IReadOnlyList<MissionListRow> Build(
        IReadOnlyList<FanMission> filteredSorted,
        IReadOnlyList<FanMission> allMissions,
        IReadOnlyList<Series> series,
        SortField sortField,
        bool ascending)
    {
        var seriesById = series.ToDictionary(s => s.Id);
        var units = new List<Unit>();
        var groups = new Dictionary<int, GroupUnit>();

        foreach (var mission in filteredSorted)
        {
            if (mission.SeriesId is int seriesId && seriesById.TryGetValue(seriesId, out var owningSeries))
            {
                if (!groups.TryGetValue(seriesId, out var group))
                {
                    group = new GroupUnit(owningSeries);
                    groups[seriesId] = group;
                    units.Add(group);
                }
                group.Members.Add(mission);
            }
            else
            {
                units.Add(new StandaloneUnit(mission));
            }
        }

        IEnumerable<Unit> ordered = units;
        if (sortField == SortField.Title)
        {
            ordered = ascending
                ? units.OrderBy(u => u.TitleKey, StringComparer.CurrentCulture)
                : units.OrderByDescending(u => u.TitleKey, StringComparer.CurrentCulture);
        }

        var rows = new List<MissionListRow>();
        foreach (var unit in ordered)
        {
            switch (unit)
            {
                case StandaloneUnit standalone:
                    rows.Add(new MissionRow(standalone.Mission, isSeriesMember: false));
                    break;

                case GroupUnit group:
                    var owned = allMissions.Where(m => m.SeriesId == group.Series.Id).ToList();
                    var games = owned.Select(m => m.Game).Distinct().ToList();
                    rows.Add(new SeriesHeaderRow(
                        group.Series,
                        group.Members.Count,
                        owned.Count,
                        owned.Count(m => m.Status == MissionStatus.Completed),
                        games.Count == 1 ? games[0] : null));

                    if (group.Series.IsExpanded)
                    {
                        foreach (var member in group.Members
                                     .OrderBy(m => m.SeriesPosition ?? int.MaxValue)
                                     .ThenBy(m => m.Title, StringComparer.CurrentCulture))
                            rows.Add(new MissionRow(member, isSeriesMember: true));
                    }
                    break;
            }
        }

        return rows;
    }

    private abstract class Unit
    {
        public abstract string TitleKey { get; }
    }

    private sealed class StandaloneUnit : Unit
    {
        public StandaloneUnit(FanMission mission) => Mission = mission;
        public FanMission Mission { get; }
        public override string TitleKey => Mission.Title;
    }

    private sealed class GroupUnit : Unit
    {
        public GroupUnit(Series series) => Series = series;
        public Series Series { get; }
        public List<FanMission> Members { get; } = new();
        public override string TitleKey => Series.Name;
    }
}
