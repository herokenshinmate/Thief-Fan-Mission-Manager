# Game Groups and Campaign Highlighting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the main mission list into one big collapsible banner per game (collapsed state remembered), and highlight packed campaigns (gold) and series (blue) with an accent bar and a badge.

**Architecture:**
- **List building:** `MissionListBuilder` gains an outer loop over games that emits a `GameHeaderRow` per game with visible missions. It then runs today's grouping logic on that game's missions only.
- **Highlight data:** row types expose an `Accent` (`AccentKind`) and a campaign badge text.
- **Remembered state:** collapsed games are stored in two new settings columns, written through a targeted repository method.
- **List UI:** a style selector gives banners their own full-width container template, while normal rows gain the accent bar in their existing template.

**Tech Stack:** .NET 8 WPF, WPF-UI 4.3, CommunityToolkit.Mvvm 8.4, EF Core 8 + SQLite, xUnit 2.5.

**Spec:** `docs/superpowers/specs/2026-09-25-game-groups-design.md`

**Small deviations from the spec (same behaviour):**
- The spec's `IsSeriesRelated` flags are folded into the single `Accent` property, which is what the UI needs.
- The accent bar is 3 px wide and sits inside the row's existing 4 px left padding, so cells stay aligned with the column headers.
- The first banner also has a top margin (simpler template).

## Global Constraints

- **Game order:** banners follow enum order, `GameTitle.Thief1` then `GameTitle.Thief2`. A game with no visible (filtered) missions gets no banner.
- **Banner stats text:**
  - When no missions are hidden: "{N} missions · {C} completed · {I} installed".
  - When filters hide some: "{shown} of {N} missions shown · {C} completed · {I} installed".
  - Singular is "1 mission". Completed and installed count the shown missions.
- **Series spanning both games:**
  - The series header appears under each game, with that game's members; counts are per game.
  - Placeholders appear only under the game holding the lowest-positioned *shown* member (ties go to Thief1).
  - A part is missing when no owned member in any game has its position.
- **Accent colours:** Campaign #FFC9A227 (gold) takes priority over Series #FF64B5F6 (blue).
  - `MissionRow`: Campaign when `CampaignMissionCount > 1`, else Series when `IsSeriesMember`, else None.
  - `SeriesHeaderRow` and `MissingPartRow`: Series.
  - `GameHeaderRow`: None.
- **Badges:** "CAMPAIGN · {N}" (gold) and "SERIES" (blue), FontSize 10 SemiBold, on a translucent background of the same colour.
- **Persistence:** `AppSettings.Thief1Collapsed` and `Thief2Collapsed` are `INTEGER NOT NULL DEFAULT 0`. They're written only by `ISettingsRepository.SetGameCollapsedAsync`, and `SaveAsync` must never reset them.
- **Versioning:** patch bump to `3.9.4` in the last task only.
- **Code style:** file-scoped namespaces, the `Func<ThiefManagerDbContext>` repository pattern, `[ObservableProperty]` fields, and doc comments only where the reason isn't obvious.
- **Commands:**
  - Build with `dotnet build src/ThiefManager` (0 warnings).
  - Test with `dotnet test tests/ThiefManager.Tests` (baseline: 229 passing).
- **Commit trailer:** every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Don't launch the app.**

## Review Focus

1. **Saving the Settings dialog** (folders, exe paths) must not reset the collapsed games. Test: `SaveAsync_KeepsCollapsedGames` (Task 1).
2. **A Game filter that hides the game holding a series' first part** must still show that series' placeholders under the visible game. Test: the existing `Build_OwnedMemberHiddenByFilter_IsNotShownAsMissing` keeps passing, with its expectation unchanged (Task 2).
3. **Collapsing the game of the selected mission** moves the selection to that game's banner instead of clearing it. Test: `CollapsingSelectedMissionsGame_SelectsItsBanner` (Task 3).
4. **A packed campaign that is also a series member** shows gold, not blue. Test: `Rows_AccentAndCampaignBadge` (Task 2).
5. **Existing 3.9.3 databases** gain the collapsed columns on upgrade. Test: `EnsureColumns_OnPreGameGroupsDatabase_AddsCollapsedColumns` (Task 1).

---

### Task 1: Remember collapsed games in settings

**Files:**
- Modify:
  - `src/ThiefManager/Models/AppSettings.cs`
  - `src/ThiefManager/Data/ISettingsRepository.cs`
  - `src/ThiefManager/Data/SettingsRepository.cs`
  - `src/ThiefManager/Data/SchemaUpgrader.cs`
  - `tests/ThiefManager.Tests/Fakes/FakeSettingsRepository.cs`
- Test:
  - `tests/ThiefManager.Tests/Data/SettingsRepositoryTests.cs`
  - `tests/ThiefManager.Tests/Data/SchemaUpgraderTests.cs`

**Interfaces:**
- Produces:
  - `AppSettings.Thief1Collapsed`, `AppSettings.Thief2Collapsed` (bool)
  - `ISettingsRepository.SetGameCollapsedAsync(GameTitle game, bool collapsed) : Task`
  - `FakeSettingsRepository`, which implements it

- [ ] **Step 1: Add the properties and the interface method**

Append to `AppSettings`:
```csharp
    public bool Thief1Collapsed { get; set; }
    public bool Thief2Collapsed { get; set; }
```
Add to `ISettingsRepository`:
```csharp
    /// <summary>
    /// Remembers whether a game's banner in the mission list is collapsed. Writes only that
    /// column, so it never disturbs the folder settings (and SaveAsync never disturbs it).
    /// </summary>
    Task SetGameCollapsedAsync(GameTitle game, bool collapsed);
```

- [ ] **Step 2: Update the fake**

Replace `FakeSettingsRepository`'s body so that it mirrors the real repository:
- `SaveAsync` only copies the folder and exe fields onto the stored settings.
- `GetAsync` returns every field.

```csharp
    private readonly AppSettings _settings = new() { Id = 1 };

    public Task<AppSettings> GetAsync() => Task.FromResult(new AppSettings
    {
        Id = _settings.Id,
        Thief1FmFolder = _settings.Thief1FmFolder,
        Thief2FmFolder = _settings.Thief2FmFolder,
        Thief1ExePath = _settings.Thief1ExePath,
        Thief2ExePath = _settings.Thief2ExePath,
        Thief1DownloadsFolder = _settings.Thief1DownloadsFolder,
        Thief2DownloadsFolder = _settings.Thief2DownloadsFolder,
        Thief1Collapsed = _settings.Thief1Collapsed,
        Thief2Collapsed = _settings.Thief2Collapsed
    });

    public Task SaveAsync(AppSettings settings)
    {
        _settings.Thief1FmFolder = settings.Thief1FmFolder;
        _settings.Thief2FmFolder = settings.Thief2FmFolder;
        _settings.Thief1ExePath = settings.Thief1ExePath;
        _settings.Thief2ExePath = settings.Thief2ExePath;
        _settings.Thief1DownloadsFolder = settings.Thief1DownloadsFolder;
        _settings.Thief2DownloadsFolder = settings.Thief2DownloadsFolder;
        return Task.CompletedTask;
    }

    public Task SetGameCollapsedAsync(GameTitle game, bool collapsed)
    {
        if (game == GameTitle.Thief1)
            _settings.Thief1Collapsed = collapsed;
        else
            _settings.Thief2Collapsed = collapsed;
        return Task.CompletedTask;
    }
```

- [ ] **Step 3: Write the failing tests**

Add to `SettingsRepositoryTests`:
```csharp
    [Fact]
    public async Task SetGameCollapsedAsync_PersistsPerGame()
    {
        var repo = new SettingsRepository(CreateContext);

        await repo.SetGameCollapsedAsync(GameTitle.Thief2, true);

        var settings = await repo.GetAsync();
        Assert.False(settings.Thief1Collapsed);
        Assert.True(settings.Thief2Collapsed);
    }

    [Fact]
    public async Task SaveAsync_KeepsCollapsedGames()
    {
        var repo = new SettingsRepository(CreateContext);
        await repo.SaveAsync(new AppSettings { Thief1FmFolder = @"C:\fms\t1" });
        await repo.SetGameCollapsedAsync(GameTitle.Thief1, true);

        await repo.SaveAsync(new AppSettings { Thief1FmFolder = @"D:\fms\t1" });

        var settings = await repo.GetAsync();
        Assert.Equal(@"D:\fms\t1", settings.Thief1FmFolder);
        Assert.True(settings.Thief1Collapsed);
    }
```
Add to `SchemaUpgraderTests` (same pattern as the existing pre-metadata test):
```csharp
    [Fact]
    public async Task EnsureColumns_OnPreGameGroupsDatabase_AddsCollapsedColumns()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                ALTER TABLE "Settings" DROP COLUMN "Thief1Collapsed";
                ALTER TABLE "Settings" DROP COLUMN "Thief2Collapsed";
                """;
            command.ExecuteNonQuery();
        }

        SchemaUpgrader.EnsureColumns(_dbPath);

        var repo = new SettingsRepository(CreateContext);
        await repo.SetGameCollapsedAsync(GameTitle.Thief1, true);
        Assert.True((await repo.GetAsync()).Thief1Collapsed);
    }
```

- [ ] **Step 4: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter "SettingsRepositoryTests|SchemaUpgraderTests"`
Expected: a build error, because `SettingsRepository.SetGameCollapsedAsync` doesn't exist yet.

- [ ] **Step 5: Implement**

In `SchemaUpgrader.EnsureColumns`, append:
```csharp
        AddColumnIfMissing(connection, "Settings", "Thief1Collapsed", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Settings", "Thief2Collapsed", "INTEGER NOT NULL DEFAULT 0");
```
In `SettingsRepository`, add the method below. `SaveAsync` stays unchanged, because it already copies only the folder and exe fields onto an existing row.
```csharp
    public async Task SetGameCollapsedAsync(GameTitle game, bool collapsed)
    {
        using var db = _contextFactory();
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Id == SingletonId);
        if (existing is null)
        {
            existing = new AppSettings { Id = SingletonId };
            db.Settings.Add(existing);
        }

        if (game == GameTitle.Thief1)
            existing.Thief1Collapsed = collapsed;
        else
            existing.Thief2Collapsed = collapsed;
        await db.SaveChangesAsync();
    }
```

- [ ] **Step 6: Run to verify pass**

Run the filter from Step 4, then the full suite once.
Expected: all pass (229 + 3 = 232).

- [ ] **Step 7: Commit**

```bash
git add src/ThiefManager/Models/AppSettings.cs src/ThiefManager/Data tests/ThiefManager.Tests/Fakes/FakeSettingsRepository.cs tests/ThiefManager.Tests/Data
git commit -m "Remember collapsed game banners in settings"
```

---

### Task 2: Game banners and accent flags in the list builder

**Files:**
- Modify: `src/ThiefManager/ViewModels/MissionListRow.cs`, `src/ThiefManager/ViewModels/MissionListBuilder.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MissionListBuilderTests.cs`

**Interfaces:**
- Produces:
  - `enum AccentKind { None, Campaign, Series }` (in `MissionListRow.cs`)
  - `virtual AccentKind MissionListRow.Accent`
  - `MissionRow.IsPackedCampaign` and `MissionRow.CampaignBadgeText` (string?)
  - `GameHeaderRow(GameTitle game, bool isExpanded, int shownCount, int totalCount, int completedCount, int installedCount)`, with members `Game`, `IsExpanded`, `ShownCount`, `TotalCount`, `CompletedCount`, `InstalledCount`, `ChevronGlyph`, `StatsText`
  - `MissionListBuilder.Build(filteredSorted, allMissions, series, sortField, ascending, parts = null, includeMissingParts = false, IReadOnlySet<GameTitle>? collapsedGames = null)`

- [ ] **Step 1: Update the existing tests for the leading banners**

In `MissionListBuilderTests`:
- **Add this helper:**
  ```csharp
      private static IReadOnlyList<MissionListRow> WithoutBanners(IReadOnlyList<MissionListRow> rows) =>
          rows.Where(r => r is not GameHeaderRow).ToList();
  ```
- **The private `Build(...)` helper:** change its body to `WithoutBanners(MissionListBuilder.Build(filteredSorted, all, series, sortField, ascending)).Select(Describe).ToArray()`.
- **Direct `MissionListBuilder.Build(...)` calls:** in every existing test that calls it and then indexes `rows[...]` or enumerates the rows, wrap the call in `WithoutBanners(...)`. These are the tests `Build_WhenFiltersHideSomeMembers_ReportsShownOfTotal`, `Build_MemberDisplayTitle_ShowsPosition`, `Build_WithParts_InterleavesMissingPartsByPosition`, `Build_WithPartsButMissingPartsExcluded_ShowsOnlyOwnedMembers`, `Build_CollapsedSeriesWithParts_EmitsOnlyHeader`, `Build_OwnedMemberHiddenByFilter_IsNotShownAsMissing` and `Build_SeriesWithoutParts_KeepsPlainCount`. Keep their expected values exactly as they are.
- **Delete `Build_CommonGame_SetOnlyWhenAllMembersShareIt`.** A series spanning games is now split per game, and the new spanning test below replaces it.

- [ ] **Step 2: Write the new failing tests**

Add to `MissionListBuilderTests`:
```csharp
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
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionListBuilderTests`
Expected: a build error, because `GameHeaderRow`, `AccentKind` and `WithoutBanners` targets don't exist yet.

- [ ] **Step 4: Implement the rows**

In `MissionListRow.cs`, update the base class doc to mention game banners, then add:
```csharp
/// <summary>Which highlight a row's accent bar shows: packed campaigns win over series.</summary>
public enum AccentKind
{
    None,
    Campaign,
    Series
}
```
Give `MissionListRow` a member:
```csharp
    public virtual AccentKind Accent => AccentKind.None;
```
Add to `MissionRow`:
```csharp
    /// <summary>One FM that bundles several missions (Thief Guild's "Campaign of N missions").</summary>
    public bool IsPackedCampaign => Mission.CampaignMissionCount > 1;

    public string? CampaignBadgeText => IsPackedCampaign ? $"CAMPAIGN · {Mission.CampaignMissionCount}" : null;

    public override AccentKind Accent => IsPackedCampaign ? AccentKind.Campaign
        : IsSeriesMember ? AccentKind.Series
        : AccentKind.None;
```
Add `public override AccentKind Accent => AccentKind.Series;` to both `SeriesHeaderRow` and `MissingPartRow`.

Add the banner row:
```csharp
/// <summary>A game's banner at the top of its section of the list.</summary>
public sealed class GameHeaderRow : MissionListRow
{
    public GameHeaderRow(GameTitle game, bool isExpanded, int shownCount, int totalCount, int completedCount, int installedCount)
    {
        Game = game;
        IsExpanded = isExpanded;
        ShownCount = shownCount;
        TotalCount = totalCount;
        CompletedCount = completedCount;
        InstalledCount = installedCount;
    }

    public GameTitle Game { get; }
    public bool IsExpanded { get; }

    /// <summary>Missions of this game passing the current filters.</summary>
    public int ShownCount { get; }

    /// <summary>All missions of this game in the library.</summary>
    public int TotalCount { get; }

    public int CompletedCount { get; }
    public int InstalledCount { get; }

    public string ChevronGlyph => IsExpanded ? "▼" : "▶";

    public string StatsText
    {
        get
        {
            var missions = ShownCount == TotalCount
                ? Plural(TotalCount, "mission")
                : $"{ShownCount} of {Plural(TotalCount, "mission")} shown";
            return $"{missions} · {CompletedCount} completed · {InstalledCount} installed";
        }
    }

    private static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? string.Empty : "s")}";
}
```

- [ ] **Step 5: Implement the builder**

Restructure `MissionListBuilder.Build`:
- Add the parameter `IReadOnlySet<GameTitle>? collapsedGames = null` at the end.
- Move today's body (from `var seriesById` through `return rows;`) into a private method `BuildGameRows`. It runs once per game. `Build` becomes:
```csharp
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
```
- **`BuildGameRows`:** takes `(GameTitle game, List<FanMission> gameShown, IReadOnlyList<FanMission> allMissions, Dictionary<int, Series> seriesById, Dictionary<int, List<SeriesPart>> partsBySeries, Dictionary<int, GameTitle> placeholderGameBySeries, SortField sortField, bool ascending, bool includeMissingParts)` and returns `List<MissionListRow>`. Its body is today's unit and row logic, with `filteredSorted` replaced by `gameShown`, plus two changes inside `case GroupUnit group:`:
  1. **Header counts** use only this game's members:
     ```csharp
     var owned = allMissions.Where(m => m.SeriesId == group.Series.Id && m.Game == game).ToList();
     ```
     The part "missing" test must still use every game:
     ```csharp
     var ownedAnyGame = allMissions.Where(m => m.SeriesId == group.Series.Id).ToList();
     ```
     Use `ownedAnyGame` in the `.Where(p => ownedAnyGame.All(m => m.SeriesPosition != p.Position))` filter.
  2. **Placeholders** are only added when `includeMissingParts && seriesParts is not null && placeholderGameBySeries.GetValueOrDefault(group.Series.Id) == game`.
- **Doc comment:** extend `Build`'s doc with "Rows are grouped under one GameHeaderRow per game (Thief1 then Thief2) that has shown missions; a collapsed game contributes only its banner, and grouping, sorting and placeholders then apply within each game."

- [ ] **Step 6: Run to verify pass**

Run the filter, then the full suite once.
Expected:
- All `MissionListBuilderTests` pass: the old ones with unchanged expectations, and the 7 new ones.
- The full suite may have `MainViewModelTests` failures caused **only** by the leading banner. That's expected; Task 3 fixes them.
- Report the full-suite result either way.

Don't change `MainViewModelTests` in this task.

- [ ] **Step 7: Commit**

```bash
git add src/ThiefManager/ViewModels/MissionListRow.cs src/ThiefManager/ViewModels/MissionListBuilder.cs tests/ThiefManager.Tests/ViewModels/MissionListBuilderTests.cs
git commit -m "Group the mission list under per-game banners and flag campaign and series rows"
```

---

### Task 3: MainViewModel: collapsed games, banner selection and toggle

**Files:**
- Modify: `src/ThiefManager/ViewModels/MainViewModel.cs`, `src/ThiefManager/App.xaml.cs` (constructor call)
- Test: `tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `ISettingsRepository.GetAsync` / `SetGameCollapsedAsync`, `GameHeaderRow`, and `Build(…, collapsedGames)`.
- Produces:
  - The `MainViewModel` constructor gains a last parameter, `ISettingsRepository settingsRepository`.
  - `IAsyncRelayCommand<GameHeaderRow?> ToggleGameExpandedCommand` (a null parameter means the selected banner).
  - `bool IsGameHeaderSelected`.

- [ ] **Step 1: Update the test helper and the existing assertions**

In `MainViewModelTests.MakeViewModel`:
- Add the optional parameter `FakeSettingsRepository? settingsRepo = null`.
- Pass `settingsRepo ?? new FakeSettingsRepository()` as the new last constructor argument.

Some existing assertions index `vm.VisibleRows` directly: `Load_GroupsSeriesMembersUnderHeader` and `Load_ShowsMissingPartPlaceholders`. Shift those indices by one, because `VisibleRows[0]` is now the game banner, and add `Assert.IsType<GameHeaderRow>(vm.VisibleRows[0]);`. Fix any other assertion that breaks **only** because of the leading banner in the same minimal way. Don't change what a test checks.

- [ ] **Step 2: Write the failing tests**

```csharp
    [Fact]
    public async Task SelectingGameBanner_DisablesMissionCommandsAndHidesMissionMenu()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.SelectedRow = vm.VisibleRows.OfType<GameHeaderRow>().Single();

        Assert.Null(vm.SelectedMission);
        Assert.True(vm.IsGameHeaderSelected);
        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task ToggleGameExpanded_CollapsesAndPersists()
    {
        var repo = new FakeMissionRepository();
        var settings = new FakeSettingsRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo, settingsRepo: settings);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleGameExpandedCommand.ExecuteAsync(vm.VisibleRows.OfType<GameHeaderRow>().Single());

        Assert.Empty(vm.VisibleMissions);
        Assert.False(vm.VisibleRows.OfType<GameHeaderRow>().Single().IsExpanded);
        Assert.True((await settings.GetAsync()).Thief1Collapsed);
    }

    [Fact]
    public async Task Load_AppliesCollapsedGamesFromSettings()
    {
        var repo = new FakeMissionRepository();
        var settings = new FakeSettingsRepository();
        await settings.SetGameCollapsedAsync(GameTitle.Thief2, true);
        await repo.AddAsync(new FanMission { Title = "T1", Game = GameTitle.Thief1, FolderPath = "a" });
        await repo.AddAsync(new FanMission { Title = "T2", Game = GameTitle.Thief2, FolderPath = "b" });
        var vm = MakeViewModel(repo, settingsRepo: settings);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "T1" }, vm.VisibleMissions.Select(m => m.Title));
        Assert.Equal(2, vm.VisibleRows.OfType<GameHeaderRow>().Count());
    }

    [Fact]
    public async Task CollapsingSelectedMissionsGame_SelectsItsBanner()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.ToggleGameExpandedCommand.ExecuteAsync(vm.VisibleRows.OfType<GameHeaderRow>().Single());

        Assert.Equal(GameTitle.Thief1, Assert.IsType<GameHeaderRow>(vm.SelectedRow).Game);
        Assert.Null(vm.SelectedMission);
    }
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MainViewModelTests`
Expected: a build error, because the `MainViewModel` constructor doesn't take `ISettingsRepository` yet and `ToggleGameExpandedCommand` doesn't exist.

- [ ] **Step 4: Implement in `MainViewModel`**

- **Constructor:** add the parameter `ISettingsRepository settingsRepository` last, stored in `_settingsRepository`. Also add the field `private HashSet<GameTitle> _collapsedGames = new();`.
- **Command:** in the constructor, add `ToggleGameExpandedCommand = new AsyncRelayCommand<GameHeaderRow?>(ToggleGameExpandedAsync);` and declare `public IAsyncRelayCommand<GameHeaderRow?> ToggleGameExpandedCommand { get; }`.
- **New properties:** `public bool IsGameHeaderSelected => SelectedRow is GameHeaderRow;`. Change `ShowMissionMenuItems` to `SelectedRow is not SeriesHeaderRow and not MissingPartRow and not GameHeaderRow`.
- **Selection notifications:** in `OnSelectedRowChanged`, add `OnPropertyChanged(nameof(IsGameHeaderSelected));`.
- **Loading:** in `LoadAsync`, before `ApplyQuery()`, add:
  ```csharp
          var settings = await _settingsRepository.GetAsync();
          _collapsedGames = new HashSet<GameTitle>();
          if (settings.Thief1Collapsed)
              _collapsedGames.Add(GameTitle.Thief1);
          if (settings.Thief2Collapsed)
              _collapsedGames.Add(GameTitle.Thief2);
  ```
- **`ApplyQuery`:**
  - Capture these along with the existing captures:
    ```csharp
    var selectedGame = (SelectedRow as GameHeaderRow)?.Game;
    var selectedMissionGame = SelectedMission?.Game;
    ```
  - Pass `_collapsedGames` as the new last `Build` argument (`collapsedGames: _collapsedGames`).
  - Extend the re-selection chain with two final fallbacks:
    ```csharp
            ?? rows.FirstOrDefault(r => selectedGame is not null && r is GameHeaderRow g && g.Game == selectedGame)
            ?? rows.FirstOrDefault(r => selectedMissionGame is not null && r is GameHeaderRow g2 && g2.Game == selectedMissionGame);
    ```
- **Toggle method:** add
  ```csharp
      private async Task ToggleGameExpandedAsync(GameHeaderRow? header)
      {
          header ??= SelectedRow as GameHeaderRow;
          if (header is null)
              return;

          var collapse = header.IsExpanded;
          if (collapse)
              _collapsedGames.Add(header.Game);
          else
              _collapsedGames.Remove(header.Game);
          await _settingsRepository.SetGameCollapsedAsync(header.Game, collapse);
          ApplyQuery();
      }
  ```
- **App.xaml.cs:** pass `settingsRepository` as the new last `MainViewModel` constructor argument. The variable already exists there.

- [ ] **Step 5: Run to verify pass**

Run the filter, then the full suite once.
Expected: all pass (232 + 7 − 1 from Task 2, + 4 here = 242).

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/ViewModels/MainViewModel.cs src/ThiefManager/App.xaml.cs tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "Collapse and remember game banners in the mission list"
```

---

### Task 4: Wire up the UI

**Files:**
- Create: `src/ThiefManager/Converters/AccentKindToBrushConverter.cs`, `src/ThiefManager/Views/MissionListItemStyleSelector.cs`
- Modify:
  - `src/ThiefManager/Themes/DarkTheme.xaml`
  - `src/ThiefManager/App.xaml`
  - `src/ThiefManager/MainWindow.xaml`
  - `src/ThiefManager/MainWindow.xaml.cs`

**Interfaces:**
- Consumes:
  - `AccentKind`, `MissionListRow.Accent`, `MissionRow.CampaignBadgeText`
  - `GameHeaderRow` (`Game`, `ChevronGlyph`, `StatsText`)
  - `MainViewModel.ToggleGameExpandedCommand` and `IsGameHeaderSelected`

This task is XAML and code-behind only; xUnit can't exercise it. Verify it with the build, the full test suite and a careful read of the XAML. **Don't launch the app.**

- [ ] **Step 1: Theme brushes and the accent bar**

In `DarkTheme.xaml`, add these next to the other `SolidColorBrush` resources:
```xml
    <SolidColorBrush x:Key="CampaignAccentBrush" Color="#FFC9A227"/>
    <SolidColorBrush x:Key="SeriesAccentBrush" Color="#FF64B5F6"/>
    <SolidColorBrush x:Key="CampaignBadgeBackgroundBrush" Color="#33C9A227"/>
    <SolidColorBrush x:Key="SeriesBadgeBackgroundBrush" Color="#3364B5F6"/>
    <SolidColorBrush x:Key="BannerBackgroundBrush" Color="{StaticResource PanelColor}"/>
```
In `GridViewListViewItemStyle`'s `ControlTemplate`, replace the root `Border x:Name="ItemBorder" …>…</Border>` with a `Grid`. The Grid holds that same `ItemBorder`, unchanged, followed by the accent bar overlay:
```xml
                    <Grid>
                        <Border x:Name="ItemBorder"
                                Background="{TemplateBinding Background}"
                                Padding="{TemplateBinding Padding}"
                                SnapsToDevicePixels="True">
                            <GridViewRowPresenter Content="{TemplateBinding Content}"
                                                  Columns="{TemplateBinding GridView.ColumnCollection}"/>
                        </Border>
                        <!-- Campaign/series accent: sits inside the row's 4px left padding so the
                             cells stay aligned with the column headers, and outside the hover/selection
                             fill so it stays visible. -->
                        <Border Width="3" HorizontalAlignment="Left" IsHitTestVisible="False"
                                Background="{Binding Accent, Converter={StaticResource AccentKindToBrushConverter}}"/>
                    </Grid>
```
The existing triggers target `ItemBorder` by name and keep working. `AccentKindToBrushConverter` is an App-level resource. The DarkTheme dictionary is merged into App, which resolves template `StaticResource`s at use time. If the build or XAML load reports it missing, move `GridViewListViewItemStyle` into the MainWindow resources added in Step 3 and say so in the report.

- [ ] **Step 2: The converter and the style selector**

`src/ThiefManager/Converters/AccentKindToBrushConverter.cs`:
```csharp
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThiefManager.ViewModels;

namespace ThiefManager.Converters;

public class AccentKindToBrushConverter : IValueConverter
{
    public Brush? CampaignBrush { get; set; }
    public Brush? SeriesBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AccentKind.Campaign => CampaignBrush ?? Brushes.Transparent,
        AccentKind.Series => SeriesBrush ?? Brushes.Transparent,
        _ => Brushes.Transparent
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("AccentKindToBrushConverter is one-way only.");
}
```
Register it in `App.xaml` after the other converters:
```xml
            <converters:AccentKindToBrushConverter x:Key="AccentKindToBrushConverter"
                                                   CampaignBrush="{StaticResource CampaignAccentBrush}"
                                                   SeriesBrush="{StaticResource SeriesAccentBrush}"/>
```
`src/ThiefManager/Views/MissionListItemStyleSelector.cs`:
```csharp
using System.Windows;
using System.Windows.Controls;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

/// <summary>Game banners get a full-width container; every other row keeps the grid row style.</summary>
public class MissionListItemStyleSelector : StyleSelector
{
    public Style? RowStyle { get; set; }
    public Style? BannerStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container) =>
        item is GameHeaderRow ? BannerStyle : RowStyle;
}
```

- [ ] **Step 3: The banner style and the ListView (`MainWindow.xaml`)**

Directly after the root `<ui:FluentWindow …>` opening tag and before `<Grid>`, add:
```xml
    <ui:FluentWindow.Resources>
        <Style x:Key="GameBannerItemStyle" TargetType="ListViewItem">
            <Setter Property="Foreground" Value="{StaticResource ForegroundBrush}"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ListViewItem">
                        <Border x:Name="BannerBorder" Background="{StaticResource BannerBackgroundBrush}"
                                BorderBrush="{StaticResource ControlBorderBrush}" BorderThickness="1"
                                CornerRadius="4" Margin="0,6,0,4" Padding="8,6" SnapsToDevicePixels="True">
                            <StackPanel Orientation="Horizontal">
                                <Button Content="{Binding ChevronGlyph}" Style="{StaticResource SeriesChevronButtonStyle}" FontSize="14"
                                        VerticalAlignment="Center"
                                        Command="{Binding DataContext.ToggleGameExpandedCommand, RelativeSource={RelativeSource AncestorType=ListView}}"
                                        CommandParameter="{Binding}"/>
                                <Grid Width="32" Height="32" Margin="4,0,10,0" VerticalAlignment="Center">
                                    <ui:SymbolIcon Symbol="Games24" FontSize="28"
                                                   Foreground="{Binding Game, Converter={StaticResource GameTitleToBrushConverter}}"/>
                                    <Image Source="{Binding Game, Converter={StaticResource GameTitleToIconSourceConverter}}" Stretch="Uniform"/>
                                </Grid>
                                <StackPanel VerticalAlignment="Center">
                                    <TextBlock Text="{Binding Game, Converter={StaticResource GameTitleToDisplayNameConverter}}"
                                               FontSize="18" FontWeight="SemiBold"/>
                                    <TextBlock Text="{Binding StatsText}" FontSize="12" Foreground="{StaticResource MutedForegroundBrush}"/>
                                </StackPanel>
                            </StackPanel>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="BannerBorder" Property="BorderBrush" Value="{StaticResource AccentGoldBrush}"/>
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="BannerBorder" Property="BorderBrush" Value="{StaticResource AccentGoldBrush}"/>
                                <Setter TargetName="BannerBorder" Property="BorderThickness" Value="2"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </ui:FluentWindow.Resources>
```
On `MissionListView`, remove `ItemContainerStyle="{StaticResource GridViewListViewItemStyle}"` and add:
```xml
            <ListView.ItemContainerStyleSelector>
                <views:MissionListItemStyleSelector RowStyle="{StaticResource GridViewListViewItemStyle}"
                                                    BannerStyle="{StaticResource GameBannerItemStyle}"/>
            </ListView.ItemContainerStyleSelector>
```

- [ ] **Step 4: Title-cell badges**

In the Title column's `MissionTemplate`, replace the `TextBlock x:Name="TitleText"` and its `DataTemplate.Triggers` with:
```xml
                    <DataTemplate>
                        <StackPanel x:Name="TitlePanel" Orientation="Horizontal">
                            <Border Background="{StaticResource CampaignBadgeBackgroundBrush}" CornerRadius="3" Padding="5,0" Margin="0,0,6,0"
                                    VerticalAlignment="Center"
                                    Visibility="{Binding CampaignBadgeText, Converter={StaticResource NullToVisibilityConverter}}">
                                <TextBlock Text="{Binding CampaignBadgeText}" FontSize="10" FontWeight="SemiBold"
                                           Foreground="{StaticResource CampaignAccentBrush}"/>
                            </Border>
                            <TextBlock Text="{Binding DisplayTitle}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
                        </StackPanel>
                        <DataTemplate.Triggers>
                            <DataTrigger Binding="{Binding IsSeriesMember}" Value="True">
                                <Setter TargetName="TitlePanel" Property="Margin" Value="22,0,0,0"/>
                            </DataTrigger>
                        </DataTemplate.Triggers>
                    </DataTemplate>
```
In the Title column's `SeriesHeaderTemplate`, insert this between the `BookOpen24` icon and the `HeaderText` TextBlock:
```xml
                            <Border Background="{StaticResource SeriesBadgeBackgroundBrush}" CornerRadius="3" Padding="5,0" Margin="0,0,6,0"
                                    VerticalAlignment="Center">
                                <TextBlock Text="SERIES" FontSize="10" FontWeight="SemiBold" Foreground="{StaticResource SeriesAccentBrush}"/>
                            </Border>
```

- [ ] **Step 5: Context menu and double-click**

In the ListView's `ContextMenu`, add as the first item:
```xml
                    <MenuItem Header="Expand / Collapse Game" Command="{Binding ToggleGameExpandedCommand}"
                              Visibility="{Binding IsGameHeaderSelected, Converter={StaticResource BoolToVisibilityConverter}}">
                        <MenuItem.Icon>
                            <ui:SymbolIcon Symbol="ChevronUpDown24"/>
                        </MenuItem.Icon>
                    </MenuItem>
```
In `MainWindow.xaml.cs` `MissionList_DoubleClick`, directly after the chevron-button guard, add:
```csharp
        if (_viewModel.SelectedRow is GameHeaderRow)
        {
            await _viewModel.ToggleGameExpandedCommand.ExecuteAsync(null);
            return;
        }
```

- [ ] **Step 6: Build, test and read the XAML carefully**

Run `dotnet build src/ThiefManager` (expect 0 errors and 0 warnings), then `dotnet test tests/ThiefManager.Tests` (expect 242 passing).

Then read the XAML carefully to check:
- Every `StaticResource` key used exists: `CampaignAccentBrush`, `SeriesAccentBrush`, `CampaignBadgeBackgroundBrush`, `SeriesBadgeBackgroundBrush`, `BannerBackgroundBrush`, `AccentGoldBrush`, `ControlBorderBrush`, `MutedForegroundBrush`, `ForegroundBrush`, `SeriesChevronButtonStyle`, `GameTitleTo*Converter`, `NullToVisibilityConverter`, `BoolToVisibilityConverter`, `AccentKindToBrushConverter` and `GridViewListViewItemStyle`.
- The bindings match the row types.
- The ContextMenu's DataContext is the view model.

- [ ] **Step 7: Commit**

```bash
git add src/ThiefManager/Converters/AccentKindToBrushConverter.cs src/ThiefManager/Views/MissionListItemStyleSelector.cs src/ThiefManager/Themes/DarkTheme.xaml src/ThiefManager/App.xaml src/ThiefManager/MainWindow.xaml src/ThiefManager/MainWindow.xaml.cs
git commit -m "Show game banners and highlight campaigns and series in the mission list"
```

---

### Task 5: Version bump and changelog

**Files:**
- Modify: `src/ThiefManager/AppVersion.cs`, `src/ThiefManager/ChangelogEntry.cs`

- [ ] **Step 1:** Change `AppVersion.Current` to `"3.9.4"`.
- [ ] **Step 2:** Add this changelog entry at the top of `ChangelogData.Entries`:
```csharp
        new("3.9.4", "2026-09-25", new[]
        {
            "The mission list is now divided into a large banner per game (Thief 1, then Thief 2) showing how many missions you have, have completed and have installed; click a banner's arrow or double-click it to collapse that game, and the app remembers which games you collapsed.",
            "Campaigns stand out: missions that bundle several missions in one FM get a gold bar and a \"CAMPAIGN · N\" badge, and series get a blue bar with a \"SERIES\" badge on their header."
        }),
```
- [ ] **Step 3:** Run `dotnet build src/ThiefManager` and `dotnet test tests/ThiefManager.Tests`. Expect a clean build and 242 passing tests.
- [ ] **Step 4: Commit**
```bash
git add src/ThiefManager/AppVersion.cs src/ThiefManager/ChangelogEntry.cs
git commit -m "Bump to 3.9.4: game banners and campaign highlighting"
```
