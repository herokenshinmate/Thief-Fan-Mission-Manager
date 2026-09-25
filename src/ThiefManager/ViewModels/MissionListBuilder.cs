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
    /// Rows are grouped under one GameHeaderRow per game (Thief1 then Thief2) that has shown missions;
    /// a collapsed game contributes only its banner, and grouping, sorting and placeholders then apply
    /// within each game.
    /// </summary>
    public static IReadOnlyList<MissionListRow> Build(
        IReadOnlyList<FanMission> filteredSorted,
        IReadOnlyList<FanMission> allMissions,
        IReadOnlyList<Series> series,
        SortField sortField,
        bool ascending,
        IReadOnlyList<SeriesPart>? parts = null,
        bool includeMissingParts = false,
        IReadOnlySet<GameTitle>? collapsedGames = null)
    {
        var seriesById = series.ToDictionary(s => s.Id);
        var partsBySeries = (parts ?? Array.Empty<SeriesPart>())
            .GroupBy(p => p.SeriesId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Position).ToList());

        // Placeholders for a series spanning both games are listed once: under the game of its
        // lowest-positioned shown member (so a Game filter can't make them disappear).
        var placeholderGameBySeries = filteredSorted
            .Where(m => m.SeriesId is int id && seriesById.ContainsKey(id))
            .GroupBy(m => m.SeriesId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => m.SeriesPosition ?? int.MaxValue).ThenBy(m => m.Game).First().Game);

        var rows = new List<MissionListRow>();
        foreach (var game in Enum.GetValues<GameTitle>())
        {
            var gameShown = filteredSorted.Where(m => m.Game == game).ToList();
            if (gameShown.Count == 0)
                continue;

            var isExpanded = collapsedGames is null || !collapsedGames.Contains(game);
            rows.Add(new GameHeaderRow(
                game,
                isExpanded,
                gameShown.Count,
                allMissions.Count(m => m.Game == game),
                gameShown.Count(m => m.Status == MissionStatus.Completed),
                gameShown.Count(m => m.InstallStatus == InstallStatus.Installed)));

            if (isExpanded)
                rows.AddRange(BuildGameRows(game, gameShown, allMissions, seriesById, partsBySeries, placeholderGameBySeries, sortField, ascending, includeMissingParts));
        }

        return rows;
    }

    private static List<MissionListRow> BuildGameRows(
        GameTitle game,
        List<FanMission> gameShown,
        IReadOnlyList<FanMission> allMissions,
        Dictionary<int, Series> seriesById,
        Dictionary<int, List<SeriesPart>> partsBySeries,
        Dictionary<int, GameTitle> placeholderGameBySeries,
        SortField sortField,
        bool ascending,
        bool includeMissingParts)
    {
        var units = new List<Unit>();
        var groups = new Dictionary<int, GroupUnit>();

        foreach (var mission in gameShown)
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
                    var owned = allMissions.Where(m => m.SeriesId == group.Series.Id && m.Game == game).ToList();
                    var ownedAnyGame = allMissions.Where(m => m.SeriesId == group.Series.Id).ToList();
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
                        if (includeMissingParts && seriesParts is not null && placeholderGameBySeries.GetValueOrDefault(group.Series.Id) == game)
                        {
                            entries.AddRange(seriesParts
                                .Where(p => ownedAnyGame.All(m => m.SeriesPosition != p.Position))
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
