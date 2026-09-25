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

    private static SeriesPart P(int seriesId, int position, string title) =>
        new() { SeriesId = seriesId, Position = position, Title = title, ThiefGuildUrl = $"https://www.thiefguild.com/fanmissions/{position}/x" };

    private static readonly SeriesPart[] BookParts = { P(1, 1, "Dead Letter Box"), P(1, 2, "The Hidden City"), P(1, 3, "In the Lion's Den") };

    private static string Describe(MissionListRow row) => row switch
    {
        SeriesHeaderRow h => $"[{h.Series.Name}]",
        MissionRow r when r.IsSeriesMember => $"  {r.Mission.Title}",
        MissionRow r => r.Mission.Title,
        _ => "?"
    };

    private static string DescribeWithParts(MissionListRow row) => row switch
    {
        MissingPartRow p => $"  ?{p.Part.Title}",
        _ => Describe(row)
    };

    private static IReadOnlyList<MissionListRow> WithoutBanners(IReadOnlyList<MissionListRow> rows) =>
        rows.Where(r => r is not GameHeaderRow).ToList();

    private static string[] Build(IReadOnlyList<FanMission> filteredSorted, IReadOnlyList<FanMission> all,
        IReadOnlyList<Series> series, SortField sortField = SortField.Title, bool ascending = true) =>
        WithoutBanners(MissionListBuilder.Build(filteredSorted, all, series, sortField, ascending)).Select(Describe).ToArray();

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

        var rows = WithoutBanners(MissionListBuilder.Build(new[] { p1 }, new[] { p1, p2, p3 }, new[] { S(1, "Book") }, SortField.Title, true));

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

        var rows = WithoutBanners(MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true));

        Assert.Equal("#3 · Part 3", ((MissionRow)rows[1]).DisplayTitle);
        Assert.Equal("Extra", ((MissionRow)rows[2]).DisplayTitle);
    }

    [Fact]
    public void Build_WithParts_InterleavesMissingPartsByPosition()
    {
        var missions = new[] { M(1, "Part 3", 1, 3), M(2, "Part 2", 1, 2) };

        var rows = WithoutBanners(MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: true));

        Assert.Equal(new[] { "[Book]", "  ?Dead Letter Box", "  Part 2", "  Part 3" }, rows.Select(DescribeWithParts));
        Assert.Equal("Book (2 of 3 owned)", ((SeriesHeaderRow)rows[0]).HeaderText);
        Assert.Equal("#1 · Dead Letter Box — not in library", ((MissingPartRow)rows[1]).DisplayTitle);
    }

    [Fact]
    public void Build_WithPartsButMissingPartsExcluded_ShowsOnlyOwnedMembers()
    {
        var missions = new[] { M(1, "Part 3", 1, 3) };

        var rows = WithoutBanners(MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: false));

        Assert.Equal(new[] { "[Book]", "  Part 3" }, rows.Select(DescribeWithParts));
        Assert.Equal("Book (1 of 3 owned)", ((SeriesHeaderRow)rows[0]).HeaderText);
    }

    [Fact]
    public void Build_CollapsedSeriesWithParts_EmitsOnlyHeader()
    {
        var missions = new[] { M(1, "Part 3", 1, 3) };

        var rows = WithoutBanners(MissionListBuilder.Build(missions, missions, new[] { S(1, "Book", expanded: false) }, SortField.Title, true, BookParts, includeMissingParts: true));

        Assert.Equal(new[] { "[Book]" }, rows.Select(DescribeWithParts));
    }

    [Fact]
    public void Build_OwnedMemberHiddenByFilter_IsNotShownAsMissing()
    {
        var shown = M(1, "Part 3", 1, 3, game: GameTitle.Thief2);
        var hiddenByGameFilter = M(2, "Part 1", 1, 1, game: GameTitle.Thief1);

        var rows = WithoutBanners(MissionListBuilder.Build(new[] { shown }, new[] { shown, hiddenByGameFilter }, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: true));

        Assert.Equal(new[] { "[Book]", "  ?The Hidden City", "  Part 3" }, rows.Select(DescribeWithParts));
    }

    [Fact]
    public void Build_SeriesWithoutParts_KeepsPlainCount()
    {
        var missions = new[] { M(1, "P1", 1, 1) };

        var rows = WithoutBanners(MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, Array.Empty<SeriesPart>(), includeMissingParts: true));

        Assert.Equal("Book (1)", ((SeriesHeaderRow)rows[0]).HeaderText);
    }

    [Fact]
    public void MissionRow_ThiefGuildDisplays()
    {
        var rated = new MissionRow(new FanMission { ThiefGuildRating = 9.02, ThiefGuildRatingCount = 229, CampaignMissionCount = 10 }, false);
        var single = new MissionRow(new FanMission { CampaignMissionCount = 1 }, false);

        Assert.Equal("★ 9.02 (229)", rated.ThiefGuildRatingDisplay);
        Assert.Equal("Campaign · 10", rated.MissionTypeDisplay);
        Assert.Null(single.ThiefGuildRatingDisplay);
        Assert.Null(single.MissionTypeDisplay);
    }

    private static FanMission G(int id, string title, GameTitle game,
        MissionStatus status = MissionStatus.NotPlayed, InstallStatus install = InstallStatus.Installed) =>
        new() { Id = id, Title = title, Game = game, Status = status, InstallStatus = install };

    private static string DescribeAll(MissionListRow row) => row switch
    {
        GameHeaderRow g => $"=={g.Game}==",
        _ => DescribeWithParts(row)
    };

    [Fact]
    public void Build_GroupsMissionsUnderGameBanners_InGameOrder()
    {
        var missions = new[] { G(1, "T2 Mission", GameTitle.Thief2), G(2, "T1 Mission", GameTitle.Thief1) };

        var rows = MissionListBuilder.Build(missions, missions, Array.Empty<Series>(), SortField.Rating, true);

        Assert.Equal(new[] { "==Thief1==", "T1 Mission", "==Thief2==", "T2 Mission" }, rows.Select(DescribeAll));
    }

    [Fact]
    public void Build_CollapsedGame_EmitsOnlyItsBanner()
    {
        var missions = new[] { G(1, "T2 Mission", GameTitle.Thief2), G(2, "T1 Mission", GameTitle.Thief1) };

        var rows = MissionListBuilder.Build(missions, missions, Array.Empty<Series>(), SortField.Rating, true,
            collapsedGames: new HashSet<GameTitle> { GameTitle.Thief1 });

        Assert.Equal(new[] { "==Thief1==", "==Thief2==", "T2 Mission" }, rows.Select(DescribeAll));
        var banner = (GameHeaderRow)rows[0];
        Assert.False(banner.IsExpanded);
        Assert.Equal("▶", banner.ChevronGlyph);
    }

    [Fact]
    public void Build_GameWithNoShownMissions_HasNoBanner()
    {
        var t1 = G(1, "T1", GameTitle.Thief1);
        var t2 = G(2, "T2", GameTitle.Thief2);

        var rows = MissionListBuilder.Build(new[] { t2 }, new[] { t1, t2 }, Array.Empty<Series>(), SortField.Title, true);

        Assert.Equal(new[] { "==Thief2==", "T2" }, rows.Select(DescribeAll));
    }

    [Fact]
    public void Build_GameBannerStats_CountShownMissions()
    {
        var a = G(1, "A", GameTitle.Thief1, MissionStatus.Completed);
        var b = G(2, "B", GameTitle.Thief1, install: InstallStatus.NotInstalled);
        var c = G(3, "C", GameTitle.Thief1);
        var all = new[] { a, b, c };

        var full = (GameHeaderRow)MissionListBuilder.Build(all, all, Array.Empty<Series>(), SortField.Title, true)[0];
        var filtered = (GameHeaderRow)MissionListBuilder.Build(new[] { a }, all, Array.Empty<Series>(), SortField.Title, true)[0];
        var single = (GameHeaderRow)MissionListBuilder.Build(new[] { a }, new[] { a }, Array.Empty<Series>(), SortField.Title, true)[0];

        Assert.Equal("3 missions · 1 completed · 2 installed", full.StatsText);
        Assert.Equal("1 of 3 missions shown · 1 completed · 1 installed", filtered.StatsText);
        Assert.Equal("1 mission · 1 completed · 1 installed", single.StatsText);
    }

    [Fact]
    public void Build_KeepsSortOrderWithinEachGame()
    {
        // Already sorted (e.g. by rating descending) across both games.
        var sorted = new[] { G(1, "Z", GameTitle.Thief2), G(2, "Y", GameTitle.Thief1), G(3, "X", GameTitle.Thief2), G(4, "W", GameTitle.Thief1) };

        var rows = MissionListBuilder.Build(sorted, sorted, Array.Empty<Series>(), SortField.Rating, false);

        Assert.Equal(new[] { "==Thief1==", "Y", "W", "==Thief2==", "Z", "X" }, rows.Select(DescribeAll));
    }

    [Fact]
    public void Build_SeriesSpanningBothGames_ShowsUnderEachGameWithPlaceholdersOnce()
    {
        var part1 = M(1, "Part 1", 1, 1, game: GameTitle.Thief1);
        var part3 = M(3, "Part 3", 1, 3, game: GameTitle.Thief2);
        var missions = new[] { part1, part3 };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: true);

        Assert.Equal(new[] { "==Thief1==", "[Book]", "  Part 1", "  ?The Hidden City", "==Thief2==", "[Book]", "  Part 3" },
            rows.Select(DescribeAll));
        var headers = rows.OfType<SeriesHeaderRow>().ToList();
        Assert.Equal(GameTitle.Thief1, headers[0].CommonGame);
        Assert.Equal(GameTitle.Thief2, headers[1].CommonGame);
    }

    [Fact]
    public void Rows_AccentAndCampaignBadge()
    {
        var campaignInSeries = new MissionRow(new FanMission { CampaignMissionCount = 10 }, isSeriesMember: true);
        var seriesMember = new MissionRow(new FanMission { CampaignMissionCount = 1 }, isSeriesMember: true);
        var plain = new MissionRow(new FanMission(), isSeriesMember: false);

        Assert.Equal(AccentKind.Campaign, campaignInSeries.Accent);
        Assert.True(campaignInSeries.IsPackedCampaign);
        Assert.Equal("CAMPAIGN · 10", campaignInSeries.CampaignBadgeText);
        Assert.Equal(AccentKind.Series, seriesMember.Accent);
        Assert.Null(seriesMember.CampaignBadgeText);
        Assert.Equal(AccentKind.None, plain.Accent);
        Assert.Equal(AccentKind.Series, new SeriesHeaderRow(S(1, "Book"), 1, 1, 0, null).Accent);
        Assert.Equal(AccentKind.Series, new MissingPartRow(P(1, 1, "Part 1")).Accent);
        Assert.Equal(AccentKind.None, new GameHeaderRow(GameTitle.Thief1, true, 1, 1, 0, 0).Accent);
    }
}
