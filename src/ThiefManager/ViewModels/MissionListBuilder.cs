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
    /// When <paramref name="includeMissingParts"/> is set, an expanded series with a known part list also lists, in position order, placeholders for the parts no owned mission occupies (checked against every owned member, not just the filtered ones).
    /// </summary>
    public static IReadOnlyList<MissionListRow> Build(
        IReadOnlyList<FanMission> filteredSorted,
        IReadOnlyList<FanMission> allMissions,
        IReadOnlyList<Series> series,
        SortField sortField,
        bool ascending,
        IReadOnlyList<SeriesPart>? parts = null,
        bool includeMissingParts = false)
    {
        var seriesById = series.ToDictionary(s => s.Id);
        var units = new List<Unit>();
        var groups = new Dictionary<int, GroupUnit>();
        var partsBySeries = (parts ?? Array.Empty<SeriesPart>())
            .GroupBy(p => p.SeriesId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Position).ToList());

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
                    partsBySeries.TryGetValue(group.Series.Id, out var seriesParts);
                    rows.Add(new SeriesHeaderRow(
                        group.Series,
                        group.Members.Count,
                        owned.Count,
                        owned.Count(m => m.Status == MissionStatus.Completed),
                        games.Count == 1 ? games[0] : null,
                        seriesParts?.Count));

                    if (group.Series.IsExpanded)
                    {
                        var entries = group.Members
                            .Select(m => (Position: m.SeriesPosition, Title: m.Title, Row: (MissionListRow)new MissionRow(m, isSeriesMember: true)))
                            .ToList();
                        if (includeMissingParts && seriesParts is not null)
                        {
                            entries.AddRange(seriesParts
                                .Where(p => owned.All(m => m.SeriesPosition != p.Position))
                                .Select(p => (Position: (int?)p.Position, Title: p.Title, Row: (MissionListRow)new MissingPartRow(p))));
                        }

                        foreach (var entry in entries
                                     .OrderBy(e => e.Position ?? int.MaxValue)
                                     .ThenBy(e => e.Title, StringComparer.CurrentCulture))
                            rows.Add(entry.Row);
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
