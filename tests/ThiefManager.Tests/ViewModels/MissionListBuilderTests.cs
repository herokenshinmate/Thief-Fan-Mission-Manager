using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class MissionListBuilderTests
{
    private static FanMission M(int id, string title, int? seriesId = null, int? position = null,
        MissionStatus status = MissionStatus.NotPlayed, GameTitle game = GameTitle.Thief2) =>
        new() { Id = id, Title = title, SeriesId = seriesId, SeriesPosition = position, Status = status, Game = game };

    private static Series S(int id, string name, bool expanded = true) => new() { Id = id, Name = name, IsExpanded = expanded };

    private static string Describe(MissionListRow row) => row switch
    {
        SeriesHeaderRow h => $"[{h.Series.Name}]",
        MissionRow r when r.IsSeriesMember => $"  {r.Mission.Title}",
        MissionRow r => r.Mission.Title,
        _ => "?"
    };

    private static string[] Build(IReadOnlyList<FanMission> filteredSorted, IReadOnlyList<FanMission> all,
        IReadOnlyList<Series> series, SortField sortField = SortField.Title, bool ascending = true) =>
        MissionListBuilder.Build(filteredSorted, all, series, sortField, ascending).Select(Describe).ToArray();

    [Fact]
    public void Build_WithNoSeries_KeepsGivenOrder()
    {
        var missions = new[] { M(1, "B"), M(2, "A") };

        Assert.Equal(new[] { "B", "A" }, Build(missions, missions, Array.Empty<Series>(), SortField.Rating));
    }

    [Fact]
    public void Build_GroupsMembersUnderHeader_OrderedByPosition()
    {
        var p3 = M(1, "Part 3", seriesId: 1, position: 3);
        var p2 = M(2, "Part 2", seriesId: 1, position: 2);
        var missions = new[] { p3, p2 };

        Assert.Equal(new[] { "[Book]", "  Part 2", "  Part 3" }, Build(missions, missions, new[] { S(1, "Book") }));
    }

    [Fact]
    public void Build_TitleSort_PlacesSeriesBySeriesName()
    {
        var a = M(1, "Alpha");
        var c = M(2, "Cistern");
        var member = M(3, "Zed Part 1", seriesId: 1, position: 1);
        var sortedByTitle = new[] { a, c, member };

        Assert.Equal(new[] { "Alpha", "[Book]", "  Zed Part 1", "Cistern" },
            Build(sortedByTitle, sortedByTitle, new[] { S(1, "Book") }, SortField.Title, ascending: true));
    }

    [Fact]
    public void Build_TitleSortDescending_PlacesSeriesBySeriesName()
    {
        var a = M(1, "Alpha");
        var c = M(2, "Cistern");
        var member = M(3, "Zed Part 1", seriesId: 1, position: 1);
        var sortedByTitleDesc = new[] { member, c, a };

        Assert.Equal(new[] { "Cistern", "[Book]", "  Zed Part 1", "Alpha" },
            Build(sortedByTitleDesc, sortedByTitleDesc, new[] { S(1, "Book") }, SortField.Title, ascending: false));
    }

    [Fact]
    public void Build_NonTitleSort_PlacesSeriesAtItsFirstMember()
    {
        // Already sorted by rating descending: X, P2, Y, P3.
        var sorted = new[] { M(1, "X"), M(2, "P2", 1, 2), M(3, "Y"), M(4, "P3", 1, 3) };

        Assert.Equal(new[] { "X", "[Book]", "  P2", "  P3", "Y" },
            Build(sorted, sorted, new[] { S(1, "Book") }, SortField.Rating, ascending: false));
    }

    [Fact]
    public void Build_WhenFiltersHideSomeMembers_ReportsShownOfTotal()
    {
        var p1 = M(1, "P1", 1, 1);
        var p2 = M(2, "P2", 1, 2, MissionStatus.Completed);
        var p3 = M(3, "P3", 1, 3);

        var rows = MissionListBuilder.Build(new[] { p1 }, new[] { p1, p2, p3 }, new[] { S(1, "Book") }, SortField.Title, true);

        var header = Assert.IsType<SeriesHeaderRow>(rows[0]);
        Assert.Equal(1, header.ShownCount);
        Assert.Equal(3, header.TotalCount);
        Assert.Equal("Book (1 of 3 shown)", header.HeaderText);
        Assert.Equal("1/3 completed", header.ProgressText);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void Build_SeriesWithNoMatchingMembers_IsOmitted()
    {
        var standalone = M(1, "Alone");
        var member = M(2, "P1", 1, 1);

        Assert.Equal(new[] { "Alone" }, Build(new[] { standalone }, new[] { standalone, member }, new[] { S(1, "Book") }));
    }

    [Fact]
    public void Build_CollapsedSeries_EmitsOnlyHeader()
    {
        var missions = new[] { M(1, "P1", 1, 1), M(2, "P2", 1, 2) };

        Assert.Equal(new[] { "[Book]" }, Build(missions, missions, new[] { S(1, "Book", expanded: false) }));
    }

    [Fact]
    public void Build_UnknownSeriesId_TreatedAsStandalone()
    {
        var missions = new[] { M(1, "Lost", seriesId: 99, position: 1) };

        Assert.Equal(new[] { "Lost" }, Build(missions, missions, Array.Empty<Series>()));
    }

    [Fact]
    public void Build_NullPositionsSortLast_ThenByTitle()
    {
        var missions = new[] { M(1, "Bonus", 1, null), M(2, "Aside", 1, null), M(3, "Main", 1, 1) };

        Assert.Equal(new[] { "[Book]", "  Main", "  Aside", "  Bonus" }, Build(missions, missions, new[] { S(1, "Book") }));
    }

    [Fact]
    public void Build_MemberDisplayTitle_ShowsPosition()
    {
        var missions = new[] { M(1, "Part 3", 1, 3), M(2, "Extra", 1, null) };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true);

        Assert.Equal("#3 · Part 3", ((MissionRow)rows[1]).DisplayTitle);
        Assert.Equal("Extra", ((MissionRow)rows[2]).DisplayTitle);
    }

    [Fact]
    public void Build_CommonGame_SetOnlyWhenAllMembersShareIt()
    {
        var same = new[] { M(1, "A1", 1, 1, game: GameTitle.Thief1), M(2, "A2", 1, 2, game: GameTitle.Thief1) };
        var mixed = new[] { M(3, "B1", 2, 1, game: GameTitle.Thief1), M(4, "B2", 2, 2, game: GameTitle.Thief2) };
        var all = same.Concat(mixed).ToArray();

        var headers = MissionListBuilder.Build(all, all, new[] { S(1, "A"), S(2, "B") }, SortField.Title, true)
            .OfType<SeriesHeaderRow>().ToList();

        Assert.Equal(GameTitle.Thief1, headers.Single(h => h.Series.Name == "A").CommonGame);
        Assert.Null(headers.Single(h => h.Series.Name == "B").CommonGame);
    }
}
