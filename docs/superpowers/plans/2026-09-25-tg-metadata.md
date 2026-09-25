# Richer Thief Guild Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Store and show a mission's Thief Guild rating, mission type, description and sequel links, show dimmed placeholder rows for series parts the user doesn't own, and add a manual "Refresh Thief Guild Data" action.

**Architecture:**
- **Parser:** `ThiefGuildPageParser` extracts the new fields from the detail page the app already fetches.
- **Applying data:** a pure `ThiefGuildMetadataApplier` copies them onto a mission, `SeriesAssigner` also stores the series' complete part list in a new `SeriesParts` table, and a new metadata version column tells the startup backfill which missions to re-fetch.
- **Refresh:** the backfill gains a `refreshAll` mode for the menu action.
- **List:** `MissionListBuilder` emits a new `MissingPartRow` for unowned parts.

**Tech Stack:** .NET 8 WPF, WPF-UI 4.3, CommunityToolkit.Mvvm 8.4, EF Core 8 + SQLite, AngleSharp 1.8, System.Text.Json, xUnit 2.5.

**Spec:** `docs/superpowers/specs/2026-09-25-tg-metadata-design.md`

## Global Constraints

- **Schema:** `Database.EnsureCreated()` builds new databases; `SchemaUpgrader.EnsureColumns` must bring old ones (3.9.2 schema) up to date. No EF migrations.
- **Metadata version:** `ThiefGuildMetadata.CurrentVersion = 2`. Missions from 3.9.2 and earlier are version 0.
- **What overwrites what:**
  - The Thief Guild fields (`ThiefGuildRating`, `ThiefGuildRatingCount`, `CampaignMissionCount`, `Description`, `SequelOfTitle`, `SequelOfUrl`, `HasSequelTitle`, `HasSequelUrl`) are overwritten on every fetch, and a null in the result clears them.
  - `Author`, `ReleaseYear` and `Tags` only fill blanks.
  - Automatic series assignment only happens when `SeriesId == null && !SeriesLookupChecked`.
  - A fetch without series info never deletes stored parts or the series link.
- **Politeness:** backfill and refresh requests run one at a time, with a 1-second delay between them.
- **Parser safety:** extractors never throw on unexpected markup; they return null. Numbers are parsed with `CultureInfo.InvariantCulture`.
- **Series URL format:** `https://www.thiefguild.com/fanmissions?series={id}`. Relative Thief Guild hrefs are made absolute against `https://www.thiefguild.com`.
- **Versioning:** a patch bump to `3.9.3`, done in Task 10 only.
- **Code style:** file-scoped namespaces, the `Func<ThiefManagerDbContext>` repository pattern, `[ObservableProperty]` fields, doc comments only where the reason isn't obvious.
- **Commands:**
  - Build: `dotnet build src/ThiefManager` (0 warnings).
  - Tests: `dotnet test tests/ThiefManager.Tests` (baseline 174 passing).
- **Commit trailer:** every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Don't launch the app:** it would upgrade the user's real database. The user does the manual run.

## Review Focus

1. **First launch after upgrading.** Every linked mission is version 0 and gets re-fetched once. A series the user ungrouped in 3.9.1/3.9.2 has `SeriesLookupChecked = true` and must not be regrouped. Test: `RunAsync_DoesNotRegroupAnUngroupedMission` (Task 8).
2. **Saving Properties after upgrading.** Every new column must survive a load and save round-trip (the 3.9.2 bug class). Test: `SaveCommand_RoundTripsThiefGuildColumns` (Task 7).
3. **A refresh whose page has no series block** (a Thief Guild hiccup or a changed page) must keep the stored part list and the series link. Test: `RunAsync_ResultWithoutSeries_KeepsStoredParts` (Task 8).
4. **A Game filter hiding an owned part** (e.g. a series spanning T1 and T2) must not show that part as "not in library". Test: `Build_OwnedMemberHiddenByFilter_IsNotShownAsMissing` (Task 5).
5. **A user whose Windows locale uses a decimal comma** must still parse "9.02" as 9.02. Test: `ExtractRating_UnderCommaDecimalCulture_ParsesInvariantly` (Task 2).

---

### Task 1: Data model, schema upgrade and repositories

**Files:**
- Create: `src/ThiefManager/Models/SeriesPart.cs`, `src/ThiefManager/Models/ThiefGuildMetadata.cs`
- Modify:
  - `src/ThiefManager/Models/FanMission.cs`
  - `src/ThiefManager/Data/ThiefManagerDbContext.cs`
  - `src/ThiefManager/Data/SchemaUpgrader.cs`
  - `src/ThiefManager/Data/ISeriesRepository.cs`, `SeriesRepository.cs`
  - `src/ThiefManager/Data/IMissionRepository.cs`, `MissionRepository.cs`
  - `tests/ThiefManager.Tests/Fakes/FakeSeriesRepository.cs`, `FakeMissionRepository.cs`
- Test:
  - `tests/ThiefManager.Tests/Data/SeriesRepositoryTests.cs`, `MissionRepositoryTests.cs`, `SchemaUpgraderTests.cs`

**Interfaces:**
- Produces:
  - `SeriesPart { int Id; int SeriesId; int Position; string Title; string? ThiefGuildUrl }`
  - `ThiefGuildMetadata.CurrentVersion` (const int = 2)
  - New `FanMission` properties: `double? ThiefGuildRating`, `int? ThiefGuildRatingCount`, `int? CampaignMissionCount`, `string? Description`, `string? SequelOfTitle`, `string? SequelOfUrl`, `string? HasSequelTitle`, `string? HasSequelUrl`, `int ThiefGuildMetadataVersion`
  - `ISeriesRepository.GetAllPartsAsync() : Task<List<SeriesPart>>`
  - `ISeriesRepository.ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)`
  - `IMissionRepository.ApplyThiefGuildMetadataAsync(FanMission fetched)`, which **replaces** `ApplySeriesLookupAsync`
  - `FakeSeriesRepository.PartsList` (`List<SeriesPart>`)

- [ ] **Step 1: Add the model types**

`src/ThiefManager/Models/SeriesPart.cs`:
```csharp
namespace ThiefManager.Models;

/// <summary>One entry of a series' complete part list as Thief Guild lists it, owned or not.</summary>
public class SeriesPart
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ThiefGuildUrl { get; set; }
}
```

`src/ThiefManager/Models/ThiefGuildMetadata.cs`:
```csharp
namespace ThiefManager.Models;

public static class ThiefGuildMetadata
{
    /// <summary>
    /// Stamped on a mission when its Thief Guild data was fetched by the current parser. The
    /// startup backfill re-fetches any linked mission below this, so raise it whenever the
    /// parser starts capturing a new field.
    /// </summary>
    public const int CurrentVersion = 2;
}
```

Append to `FanMission` (after `SeriesLookupChecked`):
```csharp
    public double? ThiefGuildRating { get; set; }
    public int? ThiefGuildRatingCount { get; set; }
    public int? CampaignMissionCount { get; set; }
    public string? Description { get; set; }
    public string? SequelOfTitle { get; set; }
    public string? SequelOfUrl { get; set; }
    public string? HasSequelTitle { get; set; }
    public string? HasSequelUrl { get; set; }
    public int ThiefGuildMetadataVersion { get; set; }
```

Add to `ISeriesRepository` (after `SetExpandedAsync`):
```csharp
    Task<List<SeriesPart>> GetAllPartsAsync();

    /// <summary>Replaces the series' stored part list with <paramref name="parts"/> in one save.</summary>
    Task ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts);
```
Update the `DeleteAsync` and `DeleteOrphansAsync` doc comments to add: "Its stored part list is deleted too."

In `IMissionRepository`, **replace** `ApplySeriesLookupAsync` (method and doc comment) with:
```csharp
    /// <summary>
    /// Records a Thief Guild fetch by writing only the columns a fetch owns, so a caller holding an
    /// older copy of the mission can't revert fields the user has since edited: the Thief Guild
    /// fields and ThiefGuildMetadataVersion are copied from <paramref name="fetched"/>; Author,
    /// ReleaseYear and Tags only fill blanks; the series is only assigned when the stored mission
    /// has no series and SeriesLookupChecked is false; SeriesLookupChecked is OR-ed in. A missing
    /// mission is ignored.
    /// </summary>
    Task ApplyThiefGuildMetadataAsync(FanMission fetched);
```

- [ ] **Step 2: Update the fakes**

In `FakeMissionRepository`, replace `ApplySeriesLookupAsync` with:
```csharp
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
```
(When a test passes the same instance it stored, which is the fake's normal case, the copy is a no-op apart from the guarded series fields. That's fine.)

In `FakeSeriesRepository`:
- Add `public List<SeriesPart> PartsList { get; } = new();`
- Add these two methods:
```csharp
    public Task<List<SeriesPart>> GetAllPartsAsync() => Task.FromResult(PartsList.ToList());

    public Task ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)
    {
        PartsList.RemoveAll(p => p.SeriesId == seriesId);
        var nextId = PartsList.Count == 0 ? 1 : PartsList.Max(p => p.Id) + 1;
        foreach (var part in parts)
            PartsList.Add(new SeriesPart { Id = nextId++, SeriesId = seriesId, Position = part.Position, Title = part.Title, ThiefGuildUrl = part.ThiefGuildUrl });
        return Task.CompletedTask;
    }
```
- In `DeleteAsync(int id)`, add `PartsList.RemoveAll(p => p.SeriesId == id);`.
- In `DeleteOrphansAsync`, add `PartsList.RemoveAll(p => SeriesList.All(s => s.Id != p.SeriesId));` after the `SeriesList.RemoveAll(...)` line.

- [ ] **Step 3: Write the failing tests**

In `MissionRepositoryTests`, **replace** the two `ApplySeriesLookupAsync_*` tests with:
```csharp
    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_WritesOnlyFetchOwnedColumns()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief2, FolderPath = "m" });
        var fetched = (await repo.GetAllAsync()).Single();
        var userCopy = (await repo.GetAllAsync()).Single();
        userCopy.Status = MissionStatus.Completed;
        userCopy.Rating = 4;
        await repo.UpdateAsync(userCopy);

        fetched.ThiefGuildRating = 9.02;
        fetched.ThiefGuildRatingCount = 229;
        fetched.CampaignMissionCount = 1;
        fetched.Description = "A rainy night.";
        fetched.SequelOfTitle = "Between These Dark Walls";
        fetched.SequelOfUrl = "https://www.thiefguild.com/works/a";
        fetched.HasSequelTitle = "The Chalice of Souls";
        fetched.HasSequelUrl = "https://www.thiefguild.com/works/b";
        fetched.ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion;
        fetched.SeriesId = 7;
        fetched.SeriesPosition = 3;
        fetched.SeriesLookupChecked = true;
        await repo.ApplyThiefGuildMetadataAsync(fetched);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(MissionStatus.Completed, reloaded.Status);
        Assert.Equal(4, reloaded.Rating);
        Assert.Equal(9.02, reloaded.ThiefGuildRating);
        Assert.Equal(229, reloaded.ThiefGuildRatingCount);
        Assert.Equal(1, reloaded.CampaignMissionCount);
        Assert.Equal("A rainy night.", reloaded.Description);
        Assert.Equal("Between These Dark Walls", reloaded.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", reloaded.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", reloaded.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", reloaded.HasSequelUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, reloaded.ThiefGuildMetadataVersion);
        Assert.Equal(7, reloaded.SeriesId);
        Assert.Equal(3, reloaded.SeriesPosition);
        Assert.True(reloaded.SeriesLookupChecked);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_FillsOnlyBlankAuthorYearAndTags()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", FolderPath = "m", Author = "Mine", Tags = "" });
        var fetched = (await repo.GetAllAsync()).Single();
        fetched.Author = "Theirs";
        fetched.ReleaseYear = 2014;
        fetched.Tags = "City";

        await repo.ApplyThiefGuildMetadataAsync(fetched);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal("Mine", reloaded.Author);
        Assert.Equal(2014, reloaded.ReleaseYear);
        Assert.Equal("City", reloaded.Tags);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_DoesNotAssignSeriesToCheckedOrGroupedMission()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "Grouped", FolderPath = "g", SeriesId = 1, SeriesPosition = 9 });
        await repo.AddAsync(new FanMission { Title = "Ungrouped", FolderPath = "u", SeriesLookupChecked = true });
        foreach (var fetched in await repo.GetAllAsync())
        {
            fetched.SeriesId = 2;
            fetched.SeriesPosition = 3;
            await repo.ApplyThiefGuildMetadataAsync(fetched);
        }

        var all = await repo.GetAllAsync();
        Assert.Equal(1, all.Single(m => m.Title == "Grouped").SeriesId);
        Assert.Equal(9, all.Single(m => m.Title == "Grouped").SeriesPosition);
        Assert.Null(all.Single(m => m.Title == "Ungrouped").SeriesId);
    }
```
In the loop above, "Grouped" is loaded with `SeriesId = 1` and then set to 2 in memory. The repository must not write that change, because the stored `SeriesId` isn't null; that's what the test checks.

Add to `SeriesRepositoryTests`:
```csharp
    [Fact]
    public async Task ReplacePartsAsync_ReplacesTheSeriesPartList()
    {
        var repo = new SeriesRepository(CreateContext);
        var series = await repo.GetOrCreateByNameAsync("S");
        var other = await repo.GetOrCreateByNameAsync("Other");
        await repo.ReplacePartsAsync(other.Id, new[] { new SeriesPart { Position = 1, Title = "Keep me" } });
        await repo.ReplacePartsAsync(series.Id, new[] { new SeriesPart { Position = 1, Title = "Old" } });

        await repo.ReplacePartsAsync(series.Id, new[]
        {
            new SeriesPart { Position = 1, Title = "Part 1", ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/1/a" },
            new SeriesPart { Position = 2, Title = "Part 2" }
        });

        var parts = await repo.GetAllPartsAsync();
        Assert.Equal(new[] { "Part 1", "Part 2" }, parts.Where(p => p.SeriesId == series.Id).OrderBy(p => p.Position).Select(p => p.Title));
        Assert.Equal("https://www.thiefguild.com/fanmissions/1/a", parts.Single(p => p.Title == "Part 1").ThiefGuildUrl);
        Assert.Equal("Keep me", parts.Single(p => p.SeriesId == other.Id).Title);
    }

    [Fact]
    public async Task DeleteAsync_And_DeleteOrphansAsync_RemoveTheSeriesParts()
    {
        var repo = new SeriesRepository(CreateContext);
        var missions = new MissionRepository(CreateContext);
        var ungrouped = await repo.GetOrCreateByNameAsync("Ungrouped");
        var orphan = await repo.GetOrCreateByNameAsync("Orphan");
        var kept = await repo.GetOrCreateByNameAsync("Kept");
        await missions.AddAsync(new FanMission { Title = "M", FolderPath = "m", SeriesId = kept.Id });
        foreach (var s in new[] { ungrouped, orphan, kept })
            await repo.ReplacePartsAsync(s.Id, new[] { new SeriesPart { Position = 1, Title = s.Name } });

        await repo.DeleteAsync(ungrouped.Id);
        await repo.DeleteOrphansAsync();

        Assert.Equal("Kept", Assert.Single(await repo.GetAllPartsAsync()).Title);
    }
```

In `SchemaUpgraderTests`, add:
```csharp
    [Fact]
    public async Task EnsureColumns_OnPreMetadataDatabase_AddsMetadataSchema()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE "SeriesParts";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildRating";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildRatingCount";
                ALTER TABLE "FanMissions" DROP COLUMN "CampaignMissionCount";
                ALTER TABLE "FanMissions" DROP COLUMN "Description";
                ALTER TABLE "FanMissions" DROP COLUMN "SequelOfTitle";
                ALTER TABLE "FanMissions" DROP COLUMN "SequelOfUrl";
                ALTER TABLE "FanMissions" DROP COLUMN "HasSequelTitle";
                ALTER TABLE "FanMissions" DROP COLUMN "HasSequelUrl";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildMetadataVersion";
                """;
            command.ExecuteNonQuery();
        }

        SchemaUpgrader.EnsureColumns(_dbPath);

        var missionRepo = new MissionRepository(CreateContext);
        var seriesRepo = new SeriesRepository(CreateContext);
        await missionRepo.AddAsync(new FanMission { Title = "M", FolderPath = "m", ThiefGuildRating = 9.5, Description = "d" });
        var series = await seriesRepo.GetOrCreateByNameAsync("S");
        await seriesRepo.ReplacePartsAsync(series.Id, new[] { new SeriesPart { Position = 1, Title = "P1" } });
        var mission = Assert.Single(await missionRepo.GetAllAsync());
        Assert.Equal(9.5, mission.ThiefGuildRating);
        Assert.Equal(0, mission.ThiefGuildMetadataVersion);
        Assert.Equal("P1", Assert.Single(await seriesRepo.GetAllPartsAsync()).Title);
    }
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests`
Expected: build errors. `SeriesRepository.GetAllPartsAsync`, `ReplacePartsAsync` and `MissionRepository.ApplyThiefGuildMetadataAsync` don't exist yet, and the context has no `SeriesParts` set.

- [ ] **Step 5: Implement**

In `ThiefManagerDbContext`, add `public DbSet<SeriesPart> SeriesParts => Set<SeriesPart>();` and change `OnModelCreating` to:
```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Series>().HasIndex(s => s.ThiefGuildSeriesId).IsUnique();
        modelBuilder.Entity<SeriesPart>().HasIndex(p => p.SeriesId);
    }
```

In `SchemaUpgrader.EnsureColumns`, append after the `Series` block:
```csharp
        AddColumnIfMissing(connection, "FanMissions", "ThiefGuildRating", "REAL NULL");
        AddColumnIfMissing(connection, "FanMissions", "ThiefGuildRatingCount", "INTEGER NULL");
        AddColumnIfMissing(connection, "FanMissions", "CampaignMissionCount", "INTEGER NULL");
        AddColumnIfMissing(connection, "FanMissions", "Description", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "SequelOfTitle", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "SequelOfUrl", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "HasSequelTitle", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "HasSequelUrl", "TEXT NULL");
        AddColumnIfMissing(connection, "FanMissions", "ThiefGuildMetadataVersion", "INTEGER NOT NULL DEFAULT 0");

        CreateTableIfMissing(connection, "SeriesParts", """
            CREATE TABLE "SeriesParts" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_SeriesParts" PRIMARY KEY AUTOINCREMENT,
                "SeriesId" INTEGER NOT NULL,
                "Position" INTEGER NOT NULL,
                "Title" TEXT NOT NULL,
                "ThiefGuildUrl" TEXT NULL
            );
            CREATE INDEX "IX_SeriesParts_SeriesId" ON "SeriesParts" ("SeriesId");
            """);
```

In `MissionRepository`, replace `ApplySeriesLookupAsync` with:
```csharp
    public async Task ApplyThiefGuildMetadataAsync(FanMission fetched)
    {
        using var db = _contextFactory();
        var entity = await db.FanMissions.FindAsync(fetched.Id);
        if (entity is null)
            return;

        entity.ThiefGuildRating = fetched.ThiefGuildRating;
        entity.ThiefGuildRatingCount = fetched.ThiefGuildRatingCount;
        entity.CampaignMissionCount = fetched.CampaignMissionCount;
        entity.Description = fetched.Description;
        entity.SequelOfTitle = fetched.SequelOfTitle;
        entity.SequelOfUrl = fetched.SequelOfUrl;
        entity.HasSequelTitle = fetched.HasSequelTitle;
        entity.HasSequelUrl = fetched.HasSequelUrl;
        entity.ThiefGuildMetadataVersion = fetched.ThiefGuildMetadataVersion;
        if (string.IsNullOrWhiteSpace(entity.Author))
            entity.Author = fetched.Author;
        if (entity.ReleaseYear is null)
            entity.ReleaseYear = fetched.ReleaseYear;
        if (string.IsNullOrWhiteSpace(entity.Tags))
            entity.Tags = fetched.Tags;
        if (entity.SeriesId is null && !entity.SeriesLookupChecked)
        {
            entity.SeriesId = fetched.SeriesId;
            entity.SeriesPosition = fetched.SeriesPosition;
        }
        entity.SeriesLookupChecked |= fetched.SeriesLookupChecked;
        await db.SaveChangesAsync();
    }
```

In `SeriesRepository`, add:
```csharp
    public async Task<List<SeriesPart>> GetAllPartsAsync()
    {
        using var db = _contextFactory();
        return await db.SeriesParts.AsNoTracking().ToListAsync();
    }

    public async Task ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)
    {
        using var db = _contextFactory();
        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => p.SeriesId == seriesId).ToListAsync());
        db.SeriesParts.AddRange(parts.Select(p => new SeriesPart
        {
            SeriesId = seriesId,
            Position = p.Position,
            Title = p.Title,
            ThiefGuildUrl = p.ThiefGuildUrl
        }));
        await db.SaveChangesAsync();
    }
```
In `DeleteAsync`, before `SaveChangesAsync`, add:
```csharp
        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => p.SeriesId == id).ToListAsync());
```
In `DeleteOrphansAsync`, replace the orphan-removal tail (from `var orphans = ...` to the end) with:
```csharp
        var orphans = await db.Series.Where(s => !usedIds.Contains(s.Id)).ToListAsync();
        if (orphans.Count == 0)
            return;

        var orphanIds = orphans.Select(s => s.Id).ToList();
        db.SeriesParts.RemoveRange(await db.SeriesParts.Where(p => orphanIds.Contains(p.SeriesId)).ToListAsync());
        db.Series.RemoveRange(orphans);
        await db.SaveChangesAsync();
```

Fix the callers of the removed `ApplySeriesLookupAsync` so the build compiles. Its only production caller is `SeriesBackfillService.RunAsync`. For now, replace that line with:
```csharp
                    await _missionRepository.ApplyThiefGuildMetadataAsync(mission);
```
Task 8 rewrites this method properly.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests`
Expected: all pass. That's 174 − 2 replaced + 3 + 2 + 1 = 178.

- [ ] **Step 7: Commit**

```bash
git add src/ThiefManager/Models src/ThiefManager/Data src/ThiefManager/Services/SeriesBackfillService.cs tests/ThiefManager.Tests/Fakes tests/ThiefManager.Tests/Data
git commit -m "Add Thief Guild metadata columns and SeriesParts table"
```

---

### Task 2: Parse rating, type, description, sequels and series parts

**Files:**
- Create: `src/ThiefManager/Services/ThiefGuildLink.cs`, `src/ThiefManager/Services/ThiefGuildSeriesPartInfo.cs`
- Modify:
  - `src/ThiefManager/Services/ThiefGuildSeriesInfo.cs`
  - `src/ThiefManager/Services/ThiefGuildLookupResult.cs`
  - `src/ThiefManager/Services/ThiefGuildPageParser.cs`
- Test: `tests/ThiefManager.Tests/Services/ThiefGuildPageParserTests.cs`

**Interfaces:**
- Produces:
  - `record ThiefGuildLink(string Title, string Url)`
  - `record ThiefGuildSeriesPartInfo(int Position, string Title, string? Url)`
  - `record ThiefGuildSeriesInfo(int ThiefGuildSeriesId, string Name, int Position, IReadOnlyList<ThiefGuildSeriesPartInfo>? Parts = null)`
  - `record ThiefGuildLookupResult(string? Author, int? ReleaseYear, string Tags, string Url, ThiefGuildSeriesInfo? Series = null, double? Rating = null, int? RatingCount = null, int? CampaignMissionCount = null, string? Description = null, ThiefGuildLink? SequelOf = null, ThiefGuildLink? HasSequel = null)`
  - Public statics on `ThiefGuildPageParser`:
    - `ExtractRating(IParentNode) : (double? Rating, int? Count)`
    - `ExtractCampaignMissionCount(IParentNode) : int?`
    - `ExtractDescription(IParentNode) : string?`
    - `ExtractSidebarLink(IParentNode, string label) : ThiefGuildLink?`
    - `ExtractSeries(IParentNode, string? currentTitle = null)`

- [ ] **Step 1: Add the record types**

`ThiefGuildLink.cs`:
```csharp
namespace ThiefManager.Services;

public record ThiefGuildLink(string Title, string Url);
```
`ThiefGuildSeriesPartInfo.cs`:
```csharp
namespace ThiefManager.Services;

public record ThiefGuildSeriesPartInfo(int Position, string Title, string? Url);
```
`ThiefGuildSeriesInfo.cs`:
```csharp
namespace ThiefManager.Services;

public record ThiefGuildSeriesInfo(int ThiefGuildSeriesId, string Name, int Position, IReadOnlyList<ThiefGuildSeriesPartInfo>? Parts = null);
```
`ThiefGuildLookupResult.cs`:
```csharp
namespace ThiefManager.Services;

public record ThiefGuildLookupResult(
    string? Author,
    int? ReleaseYear,
    string Tags,
    string Url,
    ThiefGuildSeriesInfo? Series = null,
    double? Rating = null,
    int? RatingCount = null,
    int? CampaignMissionCount = null,
    string? Description = null,
    ThiefGuildLink? SequelOf = null,
    ThiefGuildLink? HasSequel = null);
```

- [ ] **Step 2: Write the failing tests**

In `ThiefGuildPageParserTests`:
- Add `using System.Globalization;` if it's missing.
- Change the two existing record-equality assertions. `ExtractSeries` now also returns `Parts`, so whole-record equality fails.
  - Replace `Assert.Equal(new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3), series);` with:
    ```csharp
            Assert.Equal(66445, series!.ThiefGuildSeriesId);
            Assert.Equal("The Book of Prophecy", series.Name);
            Assert.Equal(3, series.Position);
    ```
  - Replace `Assert.Equal(new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 2), result.Series);` with:
    ```csharp
            Assert.Equal(66445, result.Series!.ThiefGuildSeriesId);
            Assert.Equal("The Book of Prophecy", result.Series.Name);
            Assert.Equal(2, result.Series.Position);
    ```

Add this fixture and these tests, trimmed from the real Endless Rain page:
```csharp
    private const string MetadataPage = """
        <html><head><title>Endless Rain - Fan Mission for Thief Gold -  Thief Guild</title>
        <script type="application/ld+json">
        { "@context": "http://schema.org", "@type": "CreativeWork", "name": "Endless Rain",
          "description": ""There was a time"\u000D\u000AThe high plazas of Stonemarket call me tonight.   " }
        </script></head>
        <body>
          <h3>Endless Rain <span class="text-muted">(2014)</span></h3>
          <div class="panel-body"><div class="row"><div class="col-lg-6 col-xs-12"><div class="row">
            <p style="font-size: xx-large">
              <i class="material-icons text-primary">star</i>
              9.<small>02</small>
            </p></div></div>
            <div class="col"><div style="font-size: x-large">
              <a href="/fanmissions/rating_list/2535" class="label label-default">
                229 ratings
              </a></div></div></div></div>
          <ul class="list-group">
            <li class="list-group-item">Game: Thief Gold</li>
            <li class="list-group-item">Released: Sept. 8, 2014</li>
            <li class="list-group-item">
              Single mission
            </li>
            <li class="list-group-item">
              Sequel of:
              <br/>
              <a href="/works/bc79e112-64cb-4cdf-9ac5-bce4a4b82592">Between These Dark Walls</a>
            </li>
            <li class="list-group-item text-muted">
              <small>FM has a sequel:
              <br/>
              <a href="/works/242559be-45c6-41c5-8d59-34fe374f77b9">The Chalice of Souls</a>
              </small>
            </li>
          </ul>
        </body></html>
        """;

    [Fact]
    public async Task ExtractRating_ReadsRatingAndCount()
    {
        var document = await ParseAsync(MetadataPage);

        var (rating, count) = ThiefGuildPageParser.ExtractRating(document);

        Assert.Equal(9.02, rating);
        Assert.Equal(229, count);
    }

    [Fact]
    public async Task ExtractRating_UnderCommaDecimalCulture_ParsesInvariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var document = await ParseAsync(MetadataPage);

            Assert.Equal(9.02, ThiefGuildPageParser.ExtractRating(document).Rating);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task ExtractRating_WithoutRatingCountLink_ReturnsNulls()
    {
        var document = await ParseAsync("""<p><i class="material-icons">star</i> 9.<small>02</small></p>""");

        var (rating, count) = ThiefGuildPageParser.ExtractRating(document);

        Assert.Null(rating);
        Assert.Null(count);
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_SingleMission_Returns1()
    {
        var document = await ParseAsync(MetadataPage);

        Assert.Equal(1, ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_Campaign_ReturnsMissionCount()
    {
        var document = await ParseAsync("""
            <ul class="list-group"><li class="list-group-item">
                Campaign of 10 missions
            </li></ul>
            """);

        Assert.Equal(10, ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractCampaignMissionCount_Missing_ReturnsNull()
    {
        var document = await ParseAsync("""<ul class="list-group"><li class="list-group-item">Game: Thief Gold</li></ul>""");

        Assert.Null(ThiefGuildPageParser.ExtractCampaignMissionCount(document));
    }

    [Fact]
    public async Task ExtractDescription_ReadsLdJsonDescription()
    {
        var document = await ParseAsync(MetadataPage);

        var description = ThiefGuildPageParser.ExtractDescription(document);

        Assert.Equal("\"There was a time\"\r\nThe high plazas of Stonemarket call me tonight.", description);
    }

    [Fact]
    public async Task ExtractDescription_InvalidJson_ReturnsNull()
    {
        var document = await ParseAsync("""<html><head><script type="application/ld+json">{ not json</script></head><body></body></html>""");

        Assert.Null(ThiefGuildPageParser.ExtractDescription(document));
    }

    [Fact]
    public async Task ExtractSidebarLink_ReadsSequelLinksAsAbsoluteUrls()
    {
        var document = await ParseAsync(MetadataPage);

        Assert.Equal(
            new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/bc79e112-64cb-4cdf-9ac5-bce4a4b82592"),
            ThiefGuildPageParser.ExtractSidebarLink(document, "Sequel of:"));
        Assert.Equal(
            new ThiefGuildLink("The Chalice of Souls", "https://www.thiefguild.com/works/242559be-45c6-41c5-8d59-34fe374f77b9"),
            ThiefGuildPageParser.ExtractSidebarLink(document, "FM has a sequel:"));
    }

    [Fact]
    public async Task BuildResult_OnDetailPage_IncludesAllMetadata()
    {
        var document = await ParseAsync(MetadataPage);

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2535/endless-rain");

        Assert.Equal(9.02, result.Rating);
        Assert.Equal(229, result.RatingCount);
        Assert.Equal(1, result.CampaignMissionCount);
        Assert.StartsWith("\"There was a time\"", result.Description);
        Assert.Equal("Between These Dark Walls", result.SequelOf?.Title);
        Assert.Equal("The Chalice of Souls", result.HasSequel?.Title);
        Assert.Null(result.Series);
    }

    [Fact]
    public async Task BuildResult_InSeries_ListsEveryPartWithTitlesAndUrls()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC" + Part3Link));

        var result = ThiefGuildPageParser.BuildResult(document, document.Body!.TextContent, "https://www.thiefguild.com/fanmissions/2682/x");

        Assert.Equal(new[]
        {
            new ThiefGuildSeriesPartInfo(1, "The Book of Prophecy Part 1: Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/the-book-of-prophecy-part-1-dead-letter-box"),
            new ThiefGuildSeriesPartInfo(2, "Some Mission", null),
            new ThiefGuildSeriesPartInfo(3, "The Book of Prophecy Part 3: In the Lion's Den", "https://www.thiefguild.com/fanmissions/66450/the-book-of-prophecy-part-3-in-the-lions-den")
        }, result.Series!.Parts);
    }

    [Fact]
    public async Task ExtractSeries_WithoutCurrentTitle_UsesTheUnlinkedTextForTheCurrentPart()
    {
        var document = await ParseAsync(SeriesHeaderPage(Part1Link + "TBOPP2THC"));

        var series = ThiefGuildPageParser.ExtractSeries(document);

        Assert.Equal("TBOPP2THC", series!.Parts![1].Title);
    }
```
(`SeriesHeaderPage`, `Part1Link` and `Part3Link` already exist in this test class. The page title in `SeriesHeaderPage` is "Some Mission - Fan Mission for …".)

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter ThiefGuildPageParserTests`
Expected: build errors. `ExtractRating`, `ExtractCampaignMissionCount`, `ExtractDescription` and `ExtractSidebarLink` don't exist yet.

- [ ] **Step 4: Implement**

At the top of `ThiefGuildPageParser.cs`, add `using System.Globalization;` and `using System.Text.Json;`. Add this constant to the class:
```csharp
    private const string BaseUrl = "https://www.thiefguild.com";
```
Replace the `return` line of `BuildResult` with:
```csharp
        var currentTitle = scope is IDocument document ? ExtractDetailPageTitle(document) : null;
        var (rating, ratingCount) = ExtractRating(scope);

        return new ThiefGuildLookupResult(
            author, releaseYear, tags, url,
            ExtractSeries(scope, string.IsNullOrWhiteSpace(currentTitle) ? null : currentTitle),
            rating, ratingCount,
            ExtractCampaignMissionCount(scope),
            ExtractDescription(scope),
            ExtractSidebarLink(scope, "Sequel of:"),
            ExtractSidebarLink(scope, "FM has a sequel:"));
```
Add these methods:
```csharp
    /// <summary>
    /// The rating panel shows a star icon followed by the score ("9.<small>02</small>", so the
    /// paragraph's text reads "star 9.02") next to a "229 ratings" link to the rating list. Both
    /// are required; a missing half yields no rating rather than a misleading one.
    /// </summary>
    public static (double? Rating, int? Count) ExtractRating(IParentNode scope)
    {
        var countLink = scope.QuerySelector("a[href*='/fanmissions/rating_list/']");
        var countMatch = Regex.Match(countLink?.TextContent ?? string.Empty, @"(\d+)\s+ratings?");

        var starParagraph = scope.QuerySelectorAll("p")
            .FirstOrDefault(p => p.QuerySelectorAll("i.material-icons").Any(i => i.TextContent.Trim() == "star"));
        var ratingMatch = Regex.Match(starParagraph?.TextContent ?? string.Empty, @"(\d+(?:\.\d+)?)");

        if (!countMatch.Success || !ratingMatch.Success
            || !int.TryParse(countMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || !double.TryParse(ratingMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rating))
            return (null, null);

        return (rating, count);
    }

    /// <summary>The sidebar says "Single mission" or "Campaign of N missions".</summary>
    public static int? ExtractCampaignMissionCount(IParentNode scope)
    {
        foreach (var item in scope.QuerySelectorAll("li.list-group-item"))
        {
            var text = NormalizeWhitespace(item.TextContent);
            if (text.Equals("Single mission", StringComparison.OrdinalIgnoreCase))
                return 1;

            var match = Regex.Match(text, @"^Campaign of (\d+) missions?$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                return count;
        }

        return null;
    }

    /// <summary>The page's schema.org JSON block carries the description as clean, unescaped text.</summary>
    public static string? ExtractDescription(IParentNode scope)
    {
        var script = scope.QuerySelector("script[type='application/ld+json']");
        if (script is null)
            return null;

        try
        {
            using var json = JsonDocument.Parse(script.TextContent);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String)
            {
                var text = description.GetString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>
    /// A sidebar item whose text starts with <paramref name="label"/> ("Sequel of:" or "FM has a
    /// sequel:") followed by a link to the related mission.
    /// </summary>
    public static ThiefGuildLink? ExtractSidebarLink(IParentNode scope, string label)
    {
        foreach (var item in scope.QuerySelectorAll("li.list-group-item"))
        {
            if (!NormalizeWhitespace(item.TextContent).StartsWith(label, StringComparison.OrdinalIgnoreCase))
                continue;

            var link = item.QuerySelector("a[href]");
            var title = link?.TextContent.Trim();
            var href = link?.GetAttribute("href");
            return string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href) ? null : new ThiefGuildLink(title, ToAbsoluteUrl(href));
        }

        return null;
    }

    private static string NormalizeWhitespace(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string ToAbsoluteUrl(string href) =>
        href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? href
            : BaseUrl + (href.StartsWith('/') ? href : "/" + href);
```
Change `ExtractSeries`:
- New signature: `public static ThiefGuildSeriesInfo? ExtractSeries(IParentNode scope, string? currentTitle = null)`.
- Add `var parts = new List<ThiefGuildSeriesPartInfo>();` next to `memberCount`.
- In the link branch, replace `memberCount++;` (inside the `/fanmissions/` check) with:
  ```csharp
                  {
                      memberCount++;
                      var titleAttribute = element.GetAttribute("title");
                      var partTitle = string.IsNullOrWhiteSpace(titleAttribute)
                          ? element.TextContent.Trim()
                          : Regex.Replace(titleAttribute, @"\s*\(\d{4}\)\s*$", string.Empty).Trim();
                      parts.Add(new ThiefGuildSeriesPartInfo(memberCount, partTitle, ToAbsoluteUrl(href)));
                  }
  ```
- In the text branch, after `currentPosition = memberCount;`, add:
  ```csharp
                  parts.Add(new ThiefGuildSeriesPartInfo(memberCount, currentTitle ?? node.TextContent.Trim(), null));
  ```
- Change the final return to:
  ```csharp
          return currentPosition is int position ? new ThiefGuildSeriesInfo(seriesId, name, position, parts) : null;
  ```
- Append this to the method's doc comment: "Every entry is also returned as a part: other missions titled from their link's tooltip minus the trailing year, the current one titled <paramref name="currentTitle"/> (or its abbreviation when not given)."

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter ThiefGuildPageParserTests`, then the full suite once.
Expected: all pass, 178 + 12 = 190.

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/Services tests/ThiefManager.Tests/Services/ThiefGuildPageParserTests.cs
git commit -m "Parse Thief Guild rating, mission type, description, sequels and series parts"
```

---

### Task 3: Metadata applier and series-part assignment

**Files:**
- Create: `src/ThiefManager/Services/ThiefGuildMetadataApplier.cs`, `tests/ThiefManager.Tests/Services/ThiefGuildMetadataApplierTests.cs`
- Modify: `src/ThiefManager/Services/SeriesAssigner.cs`, `tests/ThiefManager.Tests/Services/SeriesAssignerTests.cs`

**Interfaces:**
- Consumes: `ThiefGuildLookupResult` (Task 2), `ISeriesRepository.ReplacePartsAsync`, `FakeSeriesRepository.PartsList` (Task 1), and `ThiefGuildMetadata.CurrentVersion`.
- Produces:
  - `static void ThiefGuildMetadataApplier.Apply(FanMission mission, ThiefGuildLookupResult result)`
  - `static Task SeriesAssigner.ApplyAsync(FanMission, ThiefGuildSeriesInfo?, ISeriesRepository)`: same signature, new rule
  - `static Task SeriesAssigner.ReplacePartsIfSameSeriesAsync(int seriesId, ThiefGuildSeriesInfo info, ISeriesRepository)`

- [ ] **Step 1: Write the failing tests**

`tests/ThiefManager.Tests/Services/ThiefGuildMetadataApplierTests.cs`:
```csharp
using ThiefManager.Models;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildMetadataApplierTests
{
    private static ThiefGuildLookupResult FullResult() => new(
        "skacky", 2014, "City, Rain", "https://www.thiefguild.com/fanmissions/2535/endless-rain",
        Rating: 9.02, RatingCount: 229, CampaignMissionCount: 1, Description: "A rainy night.",
        SequelOf: new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/a"),
        HasSequel: new ThiefGuildLink("The Chalice of Souls", "https://www.thiefguild.com/works/b"));

    [Fact]
    public void Apply_CopiesThiefGuildFieldsAndStampsVersion()
    {
        var mission = new FanMission();

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal(9.02, mission.ThiefGuildRating);
        Assert.Equal(229, mission.ThiefGuildRatingCount);
        Assert.Equal(1, mission.CampaignMissionCount);
        Assert.Equal("A rainy night.", mission.Description);
        Assert.Equal("Between These Dark Walls", mission.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", mission.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", mission.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", mission.HasSequelUrl);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2535/endless-rain", mission.ThiefGuildUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
    }

    [Fact]
    public void Apply_NullFieldsInResult_ClearStoredThiefGuildFields()
    {
        var mission = new FanMission();
        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        ThiefGuildMetadataApplier.Apply(mission, new ThiefGuildLookupResult(null, null, "", "https://www.thiefguild.com/fanmissions/2535/endless-rain"));

        Assert.Null(mission.ThiefGuildRating);
        Assert.Null(mission.ThiefGuildRatingCount);
        Assert.Null(mission.CampaignMissionCount);
        Assert.Null(mission.Description);
        Assert.Null(mission.SequelOfTitle);
        Assert.Null(mission.HasSequelUrl);
    }

    [Fact]
    public void Apply_FillsOnlyBlankAuthorYearAndTags()
    {
        var mission = new FanMission { Author = "Me", ReleaseYear = 2000, Tags = "" };

        ThiefGuildMetadataApplier.Apply(mission, FullResult());

        Assert.Equal("Me", mission.Author);
        Assert.Equal(2000, mission.ReleaseYear);
        Assert.Equal("City, Rain", mission.Tags);
    }
}
```
Add these tests to `SeriesAssignerTests`:
```csharp
    private static ThiefGuildSeriesInfo InfoWithParts(int position) => new(66445, "The Book of Prophecy", position, new[]
    {
        new ThiefGuildSeriesPartInfo(1, "Part 1", "https://www.thiefguild.com/fanmissions/2684/p1"),
        new ThiefGuildSeriesPartInfo(2, "Part 2", "https://www.thiefguild.com/fanmissions/2682/p2"),
        new ThiefGuildSeriesPartInfo(3, "Part 3", null)
    });

    [Fact]
    public async Task ApplyAsync_MissionAlreadyChecked_IsNotAssignedASeries()
    {
        var mission = new FanMission { SeriesLookupChecked = true };

        await SeriesAssigner.ApplyAsync(mission, InfoWithParts(3), _seriesRepo);

        Assert.Null(mission.SeriesId);
        Assert.Empty(_seriesRepo.SeriesList);
    }

    [Fact]
    public async Task ApplyAsync_WithParts_StoresTheSeriesPartList()
    {
        var mission = new FanMission();

        await SeriesAssigner.ApplyAsync(mission, InfoWithParts(3), _seriesRepo);

        Assert.Equal(new[] { "Part 1", "Part 2", "Part 3" },
            _seriesRepo.PartsList.Where(p => p.SeriesId == mission.SeriesId).OrderBy(p => p.Position).Select(p => p.Title));
    }

    [Fact]
    public async Task ApplyAsync_MissionInADifferentSeries_DoesNotTouchItsParts()
    {
        var manual = await _seriesRepo.GetOrCreateByNameAsync("My Own Series");
        await _seriesRepo.ReplacePartsAsync(manual.Id, new[] { new SeriesPart { Position = 1, Title = "Mine" } });
        var mission = new FanMission { SeriesId = manual.Id, SeriesPosition = 1 };

        await SeriesAssigner.ApplyAsync(mission, InfoWithParts(3), _seriesRepo);

        Assert.Equal("Mine", Assert.Single(_seriesRepo.PartsList).Title);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter "ThiefGuildMetadataApplierTests|SeriesAssignerTests"`
Expected: build error, because `ThiefGuildMetadataApplier` doesn't exist yet.

- [ ] **Step 3: Implement**

`src/ThiefManager/Services/ThiefGuildMetadataApplier.cs`:
```csharp
using ThiefManager.Models;

namespace ThiefManager.Services;

public static class ThiefGuildMetadataApplier
{
    /// <summary>
    /// Copies a lookup onto a mission in memory (the caller persists it). Rating, type,
    /// description and sequel links mirror Thief Guild and are read-only in the app, so they're
    /// always overwritten — a field the page no longer shows is cleared. Author, year and tags may
    /// have been typed by the user, so they only fill blanks. Series assignment is SeriesAssigner's.
    /// </summary>
    public static void Apply(FanMission mission, ThiefGuildLookupResult result)
    {
        mission.ThiefGuildRating = result.Rating;
        mission.ThiefGuildRatingCount = result.RatingCount;
        mission.CampaignMissionCount = result.CampaignMissionCount;
        mission.Description = result.Description;
        mission.SequelOfTitle = result.SequelOf?.Title;
        mission.SequelOfUrl = result.SequelOf?.Url;
        mission.HasSequelTitle = result.HasSequel?.Title;
        mission.HasSequelUrl = result.HasSequel?.Url;

        if (string.IsNullOrWhiteSpace(mission.Author))
            mission.Author = result.Author;
        if (mission.ReleaseYear is null)
            mission.ReleaseYear = result.ReleaseYear;
        if (string.IsNullOrWhiteSpace(mission.Tags))
            mission.Tags = result.Tags;

        mission.ThiefGuildUrl = result.Url;
        mission.ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion;
    }
}
```
Replace `SeriesAssigner.ApplyAsync` (including its doc comment), and add `ReplacePartsIfSameSeriesAsync`:
```csharp
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
```
(Keep the `using ThiefManager.Models;` import. The existing `SeriesAssignerTests` still pass: they only look up missions that haven't been checked yet.)

- [ ] **Step 4: Run to verify pass**

Run the filter from Step 2, then the full suite once.
Expected: all pass, 190 + 6 = 196.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/ThiefGuildMetadataApplier.cs src/ThiefManager/Services/SeriesAssigner.cs tests/ThiefManager.Tests/Services/ThiefGuildMetadataApplierTests.cs tests/ThiefManager.Tests/Services/SeriesAssignerTests.cs
git commit -m "Apply Thief Guild metadata to missions and store series part lists"
```

---

### Task 4: Sorting by TG Rating and Type

**Files:**
- Modify: `src/ThiefManager/Services/SortField.cs`, `src/ThiefManager/Services/MissionQuery.cs`, `src/ThiefManager/ViewModels/MainViewModel.cs` (sort options only)
- Test: `tests/ThiefManager.Tests/Services/MissionQueryTests.cs`, `tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Produces:
  - `SortField.ThiefGuildRating`, `SortField.MissionType`
  - `MainViewModel.SortFieldOptions`, now including "TG Rating" and "Type"
  - `SortFieldDisplay`, which maps them

- [ ] **Step 1: Write the failing tests**

Add to `MissionQueryTests`:
```csharp
    [Fact]
    public void Apply_SortByThiefGuildRating_PutsUnratedLastInBothDirections()
    {
        var missions = new[]
        {
            new FanMission { Title = "Unrated" },
            new FanMission { Title = "Low", ThiefGuildRating = 7.1 },
            new FanMission { Title = "High", ThiefGuildRating = 9.5 }
        };

        var ascending = MissionQuery.Apply(missions, null, null, null, SortField.ThiefGuildRating, true);
        var descending = MissionQuery.Apply(missions, null, null, null, SortField.ThiefGuildRating, false);

        Assert.Equal(new[] { "Low", "High", "Unrated" }, ascending.Select(m => m.Title));
        Assert.Equal(new[] { "High", "Low", "Unrated" }, descending.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortByMissionType_TreatsUnknownAsSingleMission()
    {
        var missions = new[]
        {
            new FanMission { Title = "Campaign", CampaignMissionCount = 10 },
            new FanMission { Title = "Unknown" },
            new FanMission { Title = "Pair", CampaignMissionCount = 2 }
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.MissionType, true);

        Assert.Equal(new[] { "Unknown", "Pair", "Campaign" }, result.Select(m => m.Title));
    }
```
Add to `MainViewModelTests`:
```csharp
    [Fact]
    public void SortFieldDisplay_MapsNewSortFields()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.SortFieldDisplay = "TG Rating";
        Assert.Equal(SortField.ThiefGuildRating, vm.SortField);
        vm.SortField = SortField.MissionType;
        Assert.Equal("Type", vm.SortFieldDisplay);
        vm.SortField = SortField.InstallStatus;
        Assert.Equal("Install Status", vm.SortFieldDisplay);
        Assert.Contains("TG Rating", vm.SortFieldOptions);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter "MissionQueryTests|MainViewModelTests"`
Expected: build error, because `SortField.ThiefGuildRating` doesn't exist yet.

- [ ] **Step 3: Implement**

Append `ThiefGuildRating` and `MissionType` to the `SortField` enum (after `Tags`).

In `MissionQuery.Apply`, add `SortField.MissionType => m.CampaignMissionCount ?? 1,` to `KeySelector`, and replace the final ordering lines with:
```csharp
        if (sortField == SortField.ThiefGuildRating)
        {
            // Unrated missions (no Thief Guild link, or no ratings yet) sink to the bottom whichever
            // direction is chosen, so sorting descending shows the best-rated first.
            var byPresence = query.OrderBy(m => m.ThiefGuildRating is null);
            query = ascending ? byPresence.ThenBy(m => m.ThiefGuildRating) : byPresence.ThenByDescending(m => m.ThiefGuildRating);
        }
        else
        {
            query = ascending ? query.OrderBy(KeySelector) : query.OrderByDescending(KeySelector);
        }

        return query.ToList();
```

In `MainViewModel`, replace `SortFieldOptions` and `SortFieldDisplay` with:
```csharp
    private static readonly (SortField Field, string Name)[] SortFieldNames =
    {
        (SortField.Title, "Title"),
        (SortField.Game, "Game"),
        (SortField.Status, "Status"),
        (SortField.InstallStatus, "Install Status"),
        (SortField.Rating, "Rating"),
        (SortField.Author, "Author"),
        (SortField.Tags, "Tags"),
        (SortField.ThiefGuildRating, "TG Rating"),
        (SortField.MissionType, "Type")
    };

    public string[] SortFieldOptions { get; } = SortFieldNames.Select(n => n.Name).ToArray();

    public string SortFieldDisplay
    {
        get => SortFieldNames.First(n => n.Field == SortField).Name;
        set => SortField = SortFieldNames.First(n => n.Name == value).Field;
    }
```
In the Sort `ComboBox` in `MainWindow.xaml`, change `Width="80"` to `Width="100"` so "TG Rating" fits. This is the only XAML change in this task.

- [ ] **Step 4: Run to verify pass**

Run the filter from Step 2, then the full suite once.
Expected: all pass, 196 + 3 = 199.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/SortField.cs src/ThiefManager/Services/MissionQuery.cs src/ThiefManager/ViewModels/MainViewModel.cs src/ThiefManager/MainWindow.xaml tests/ThiefManager.Tests/Services/MissionQueryTests.cs tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "Sort missions by Thief Guild rating and mission type"
```

---

### Task 5: Missing-part rows and new row display text

**Files:**
- Modify: `src/ThiefManager/ViewModels/MissionListRow.cs`, `src/ThiefManager/ViewModels/MissionListBuilder.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MissionListBuilderTests.cs`

**Interfaces:**
- Consumes: `SeriesPart` (Task 1)
- Produces:
  - `MissingPartRow(SeriesPart part)`, with `Part` and `DisplayTitle`
  - `MissionRow.ThiefGuildRatingDisplay` (string?) and `MissionRow.MissionTypeDisplay` (string?)
  - `SeriesHeaderRow`: a new optional constructor parameter `int? partCount = null` and a `PartCount` property
  - `MissionListBuilder.Build(filteredSorted, allMissions, series, sortField, ascending, IReadOnlyList<SeriesPart>? parts = null, bool includeMissingParts = false)`

- [ ] **Step 1: Write the failing tests**

Add to `MissionListBuilderTests`:
```csharp
    private static SeriesPart P(int seriesId, int position, string title) =>
        new() { SeriesId = seriesId, Position = position, Title = title, ThiefGuildUrl = $"https://www.thiefguild.com/fanmissions/{position}/x" };

    private static readonly SeriesPart[] BookParts = { P(1, 1, "Dead Letter Box"), P(1, 2, "The Hidden City"), P(1, 3, "In the Lion's Den") };

    private static string DescribeWithParts(MissionListRow row) => row switch
    {
        MissingPartRow p => $"  ?{p.Part.Title}",
        _ => Describe(row)
    };

    [Fact]
    public void Build_WithParts_InterleavesMissingPartsByPosition()
    {
        var missions = new[] { M(1, "Part 3", 1, 3), M(2, "Part 2", 1, 2) };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: true);

        Assert.Equal(new[] { "[Book]", "  ?Dead Letter Box", "  Part 2", "  Part 3" }, rows.Select(DescribeWithParts));
        Assert.Equal("Book (2 of 3 owned)", ((SeriesHeaderRow)rows[0]).HeaderText);
        Assert.Equal("#1 · Dead Letter Box — not in library", ((MissingPartRow)rows[1]).DisplayTitle);
    }

    [Fact]
    public void Build_WithPartsButMissingPartsExcluded_ShowsOnlyOwnedMembers()
    {
        var missions = new[] { M(1, "Part 3", 1, 3) };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: false);

        Assert.Equal(new[] { "[Book]", "  Part 3" }, rows.Select(DescribeWithParts));
        Assert.Equal("Book (1 of 3 owned)", ((SeriesHeaderRow)rows[0]).HeaderText);
    }

    [Fact]
    public void Build_CollapsedSeriesWithParts_EmitsOnlyHeader()
    {
        var missions = new[] { M(1, "Part 3", 1, 3) };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book", expanded: false) }, SortField.Title, true, BookParts, includeMissingParts: true);

        Assert.Equal(new[] { "[Book]" }, rows.Select(DescribeWithParts));
    }

    [Fact]
    public void Build_OwnedMemberHiddenByFilter_IsNotShownAsMissing()
    {
        var shown = M(1, "Part 3", 1, 3, game: GameTitle.Thief2);
        var hiddenByGameFilter = M(2, "Part 1", 1, 1, game: GameTitle.Thief1);

        var rows = MissionListBuilder.Build(new[] { shown }, new[] { shown, hiddenByGameFilter }, new[] { S(1, "Book") }, SortField.Title, true, BookParts, includeMissingParts: true);

        Assert.Equal(new[] { "[Book]", "  ?The Hidden City", "  Part 3" }, rows.Select(DescribeWithParts));
    }

    [Fact]
    public void Build_SeriesWithoutParts_KeepsPlainCount()
    {
        var missions = new[] { M(1, "P1", 1, 1) };

        var rows = MissionListBuilder.Build(missions, missions, new[] { S(1, "Book") }, SortField.Title, true, Array.Empty<SeriesPart>(), includeMissingParts: true);

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
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionListBuilderTests`
Expected: build error, because `MissingPartRow` doesn't exist yet.

- [ ] **Step 3: Implement**

In `MissionListRow.cs`:
- Add `using System.Globalization;`.
- Update the base class doc to say "a mission, a series header, or a series part the user doesn't own".
- Add to `MissionRow`:
```csharp
    public string? ThiefGuildRatingDisplay => Mission.ThiefGuildRating is double rating && Mission.ThiefGuildRatingCount is int count
        ? $"★ {rating.ToString("0.00", CultureInfo.InvariantCulture)} ({count})"
        : null;

    public string? MissionTypeDisplay => Mission.CampaignMissionCount is int missions && missions > 1
        ? $"Campaign · {missions}"
        : null;
```
- Change the `SeriesHeaderRow` constructor to take a trailing `int? partCount = null`. Store it in a new `public int? PartCount { get; }` property, documented as "How many parts Thief Guild lists for this series, or null when unknown."
- Replace `HeaderText` with:
```csharp
    public string HeaderText => ShownCount != TotalCount
        ? $"{Series.Name} ({ShownCount} of {TotalCount} shown)"
        : PartCount is int parts && parts >= TotalCount
            ? $"{Series.Name} ({TotalCount} of {parts} owned)"
            : $"{Series.Name} ({TotalCount})";
```
- Add:
```csharp
/// <summary>A part of a series the user doesn't own, shown as a dimmed placeholder under its header.</summary>
public sealed class MissingPartRow : MissionListRow
{
    public MissingPartRow(SeriesPart part) => Part = part;

    public SeriesPart Part { get; }

    public string DisplayTitle => $"#{Part.Position} · {Part.Title} — not in library";
}
```

In `MissionListBuilder.Build`:
- Add the two optional parameters `IReadOnlyList<SeriesPart>? parts = null, bool includeMissingParts = false`.
- Add a sentence to the doc comment: "When <paramref name="includeMissingParts"/> is set, an expanded series with a known part list also lists, in position order, placeholders for the parts no owned mission occupies (checked against every owned member, not just the filtered ones)."
- At the start of the method, add:
  ```csharp
          var partsBySeries = (parts ?? Array.Empty<SeriesPart>())
              .GroupBy(p => p.SeriesId)
              .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Position).ToList());
  ```
- Replace the whole `case GroupUnit group:` body with:
```csharp
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
```

- [ ] **Step 4: Run to verify pass**

Run the filter, then the full suite once.
Expected: all pass, 199 + 6 = 205. The existing builder tests are unchanged.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/ViewModels/MissionListRow.cs src/ThiefManager/ViewModels/MissionListBuilder.cs tests/ThiefManager.Tests/ViewModels/MissionListBuilderTests.cs
git commit -m "Show placeholder rows for series parts the user doesn't own"
```

---

### Task 6: MainViewModel: parts, placeholder selection, Thief Guild links, refresh state

**Files:**
- Modify: `src/ThiefManager/ViewModels/MainViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Consumes:
  - `ISeriesRepository.GetAllPartsAsync`
  - `MissionListBuilder.Build(…, parts, includeMissingParts)`
  - `MissingPartRow`
  - `ThiefGuildMetadataApplier.Apply`
  - `SeriesAssigner.ApplyAsync`
- Produces (on `MainViewModel`):
  - `bool IsMissingPartSelected`
  - `bool ShowMissionMenuItems`
  - `string? SelectedThiefGuildUrl`
  - `bool CanOpenSelectedOnThiefGuild`
  - `string? SelectedSeriesThiefGuildUrl`
  - `bool CanOpenSelectedSeriesOnThiefGuild`
  - `[ObservableProperty] bool IsThiefGuildRefreshRunning`
  - `bool CanRefreshThiefGuild`

- [ ] **Step 1: Write the failing tests**

Add to `MainViewModelTests`:
```csharp
    private static async Task<(FakeMissionRepository Repo, FakeSeriesRepository SeriesRepo, MainViewModel Vm)> MakeWithPartsAsync()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");
        await seriesRepo.ReplacePartsAsync(series.Id, new[]
        {
            new SeriesPart { Position = 1, Title = "Dead Letter Box", ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2684/p1" },
            new SeriesPart { Position = 2, Title = "The Hidden City" }
        });
        await repo.AddAsync(new FanMission { Title = "The Hidden City", Game = GameTitle.Thief2, FolderPath = "p2", SeriesId = series.Id, SeriesPosition = 2, ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2682/p2" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.SortField = SortField.Title;
        await vm.LoadCommand.ExecuteAsync(null);
        return (repo, seriesRepo, vm);
    }

    [Fact]
    public async Task Load_ShowsMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        Assert.IsType<SeriesHeaderRow>(vm.VisibleRows[0]);
        Assert.Equal("Dead Letter Box", Assert.IsType<MissingPartRow>(vm.VisibleRows[1]).Part.Title);
        Assert.IsType<MissionRow>(vm.VisibleRows[2]);
    }

    [Fact]
    public async Task StatusFilter_HidesMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.StatusFilter = MissionStatus.NotPlayed;

        Assert.Empty(vm.VisibleRows.OfType<MissingPartRow>());
    }

    [Fact]
    public async Task GameFilter_KeepsMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.GameFilter = GameTitle.Thief2;

        Assert.Single(vm.VisibleRows.OfType<MissingPartRow>());
    }

    [Fact]
    public async Task SelectingMissingPart_DisablesMissionCommandsAndOffersItsThiefGuildPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.SelectedRow = vm.VisibleRows.OfType<MissingPartRow>().Single();

        Assert.Null(vm.SelectedMission);
        Assert.True(vm.IsMissingPartSelected);
        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
        Assert.True(vm.CanOpenSelectedOnThiefGuild);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2684/p1", vm.SelectedThiefGuildUrl);
    }

    [Fact]
    public async Task SelectingMissionRow_OffersItsThiefGuildPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.SelectedMission = vm.VisibleMissions.Single();

        Assert.True(vm.ShowMissionMenuItems);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2682/p2", vm.SelectedThiefGuildUrl);
        Assert.False(vm.CanOpenSelectedSeriesOnThiefGuild);
    }

    [Fact]
    public async Task SelectingThiefGuildSeriesHeader_OffersSeriesPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.CanOpenSelectedOnThiefGuild);
        Assert.True(vm.CanOpenSelectedSeriesOnThiefGuild);
        Assert.Equal("https://www.thiefguild.com/fanmissions?series=66445", vm.SelectedSeriesThiefGuildUrl);
    }

    [Fact]
    public void IsThiefGuildRefreshRunning_TogglesCanRefresh()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.IsThiefGuildRefreshRunning = true;

        Assert.False(vm.CanRefreshThiefGuild);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_StoresMetadataAndParts()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        await repo.AddAsync(new FanMission { Title = "In the Lion's Den", FolderPath = "p3" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ApplyThiefGuildMetadataAsync(vm.VisibleMissions.Single(), new ThiefGuildLookupResult(
            "Schattengilde", 2026, "City", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3, new[]
            {
                new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
                new ThiefGuildSeriesPartInfo(2, "The Hidden City", "https://www.thiefguild.com/fanmissions/2682/p2"),
                new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
            }),
            Rating: 8.5, RatingCount: 12, CampaignMissionCount: 1));

        var mission = repo.Missions.Single();
        Assert.Equal(8.5, mission.ThiefGuildRating);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
        Assert.Equal(2, vm.VisibleRows.OfType<MissingPartRow>().Count());
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MainViewModelTests`
Expected: build error. `IsMissingPartSelected`, `ShowMissionMenuItems` and the other new members don't exist yet.

- [ ] **Step 3: Implement in `MainViewModel`**

- Add the field `private List<SeriesPart> _allParts = new();`.
- In `LoadAsync`, after loading series, add `_allParts = await _seriesRepository.GetAllPartsAsync();`.
- In `ApplyQuery`, change the `Build` call to:
```csharp
        // Placeholders aren't missions, so only show them when no mission-level filter is active;
        // the Game filter is fine since it can't make a missing part less missing.
        var includeMissingParts = StatusFilter is null && InstallStatusFilter is null
            && string.IsNullOrWhiteSpace(TagFilter) && string.IsNullOrWhiteSpace(AuthorFilter);
        var rows = MissionListBuilder.Build(filtered, _allMissions, _allSeries, SortField, SortAscending, _allParts, includeMissingParts);
```
- Add these members:
```csharp
    public bool IsMissingPartSelected => SelectedRow is MissingPartRow;

    /// <summary>Mission-specific context menu items apply to mission rows (or no selection) only.</summary>
    public bool ShowMissionMenuItems => SelectedRow is not SeriesHeaderRow and not MissingPartRow;

    public string? SelectedThiefGuildUrl => SelectedRow switch
    {
        MissingPartRow part => part.Part.ThiefGuildUrl,
        MissionRow mission => mission.Mission.ThiefGuildUrl,
        _ => null
    };

    public bool CanOpenSelectedOnThiefGuild => !string.IsNullOrWhiteSpace(SelectedThiefGuildUrl);

    public string? SelectedSeriesThiefGuildUrl => SelectedRow is SeriesHeaderRow { Series.ThiefGuildSeriesId: int seriesId }
        ? $"https://www.thiefguild.com/fanmissions?series={seriesId}"
        : null;

    public bool CanOpenSelectedSeriesOnThiefGuild => SelectedSeriesThiefGuildUrl is not null;

    /// <summary>True while the startup backfill or a manual refresh is fetching from Thief Guild.</summary>
    [ObservableProperty]
    private bool isThiefGuildRefreshRunning;

    public bool CanRefreshThiefGuild => !IsThiefGuildRefreshRunning;

    partial void OnIsThiefGuildRefreshRunningChanged(bool value) => OnPropertyChanged(nameof(CanRefreshThiefGuild));
```
- In `OnSelectedRowChanged`, after the existing `OnPropertyChanged(nameof(IsSeriesHeaderSelected));`, add:
```csharp
        OnPropertyChanged(nameof(IsMissingPartSelected));
        OnPropertyChanged(nameof(ShowMissionMenuItems));
        OnPropertyChanged(nameof(SelectedThiefGuildUrl));
        OnPropertyChanged(nameof(CanOpenSelectedOnThiefGuild));
        OnPropertyChanged(nameof(SelectedSeriesThiefGuildUrl));
        OnPropertyChanged(nameof(CanOpenSelectedSeriesOnThiefGuild));
```
- Replace the body of `ApplyThiefGuildMetadataAsync`, and change its doc comment to "Applies a successful Thief Guild lookup to a mission: see ThiefGuildMetadataApplier for which fields are overwritten versus only filled when blank, and SeriesAssigner for the series.":
```csharp
        ThiefGuildMetadataApplier.Apply(mission, result);
        await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
        await _missionRepository.UpdateAsync(mission);
        _allSeries = await _seriesRepository.GetAllAsync();
        _allParts = await _seriesRepository.GetAllPartsAsync();
        ApplyQuery();
```
- In `DeleteSelectedAsync`, after `_allSeries = await _seriesRepository.GetAllAsync();`, add `_allParts = await _seriesRepository.GetAllPartsAsync();`.

- [ ] **Step 4: Run to verify pass**

Run the filter, then the full suite once.
Expected: all pass, 205 + 8 = 213.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/ViewModels/MainViewModel.cs tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "Wire series parts, placeholder selection and Thief Guild links into MainViewModel"
```

---

### Task 7: Properties view model: Thief Guild section and full round-trip

**Files:**
- Modify: `src/ThiefManager/ViewModels/MissionEditViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MissionEditViewModelTests.cs`

**Interfaces:**
- Consumes: `ThiefGuildLink`, the new `ThiefGuildLookupResult` fields, `SeriesAssigner.ReplacePartsIfSameSeriesAsync`, and `ThiefGuildMetadata.CurrentVersion`.
- Produces (on `MissionEditViewModel`):
  - observable `Description` (string?), `SequelOf` (`ThiefGuildLink?`), `HasSequel` (`ThiefGuildLink?`) and `ThiefGuildSummary` (string?)
  - `bool ShowSequelLinks`
  - `bool HasThiefGuildInfo`

- [ ] **Step 1: Write the failing tests**

Add to `MissionEditViewModelTests` (`StubThiefGuildLookupService` and `MakeViewModel` already exist):
```csharp
    private static FanMission MissionWithThiefGuildData() => new()
    {
        Title = "Endless Rain",
        FolderPath = "er",
        ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2535/endless-rain",
        ThiefGuildRating = 9.02,
        ThiefGuildRatingCount = 229,
        CampaignMissionCount = 1,
        Description = "A rainy night.",
        SequelOfTitle = "Between These Dark Walls",
        SequelOfUrl = "https://www.thiefguild.com/works/a",
        HasSequelTitle = "The Chalice of Souls",
        HasSequelUrl = "https://www.thiefguild.com/works/b",
        ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion
    };

    [Fact]
    public async Task SaveCommand_RoundTripsThiefGuildColumns()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(MissionWithThiefGuildData());
        var vm = MakeViewModel(repo);
        vm.LoadFrom(repo.Missions.Single());
        vm.Notes = "edited";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = repo.Missions.Single();
        Assert.Equal(9.02, saved.ThiefGuildRating);
        Assert.Equal(229, saved.ThiefGuildRatingCount);
        Assert.Equal(1, saved.CampaignMissionCount);
        Assert.Equal("A rainy night.", saved.Description);
        Assert.Equal("Between These Dark Walls", saved.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", saved.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", saved.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", saved.HasSequelUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, saved.ThiefGuildMetadataVersion);
    }

    [Fact]
    public void LoadFrom_FillsThiefGuildSection()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.LoadFrom(MissionWithThiefGuildData());

        Assert.Equal("★ 9.02 from 229 ratings · Single mission", vm.ThiefGuildSummary);
        Assert.Equal("A rainy night.", vm.Description);
        Assert.Equal(new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/a"), vm.SequelOf);
        Assert.True(vm.ShowSequelLinks);
        Assert.True(vm.HasThiefGuildInfo);
    }

    [Fact]
    public void ShowSequelLinks_IsFalseForSeriesMembers()
    {
        var vm = MakeViewModel(new FakeMissionRepository());
        vm.LoadFrom(MissionWithThiefGuildData());

        vm.SeriesName = "Some Series";

        Assert.False(vm.ShowSequelLinks);
    }

    [Fact]
    public void LoadFrom_WithoutThiefGuildData_HidesTheSection()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.LoadFrom(new FanMission { Title = "Plain", FolderPath = "p" });

        Assert.Null(vm.ThiefGuildSummary);
        Assert.False(vm.HasThiefGuildInfo);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_FillsSectionAndSaveStampsVersion()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "The Black Parade", FolderPath = "bp" });
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(
            null, null, "", "https://www.thiefguild.com/fanmissions/1/the-black-parade",
            Rating: 9.7, RatingCount: 331, CampaignMissionCount: 10, Description: "A campaign."));
        var vm = MakeViewModel(repo, lookup);
        vm.LoadFrom(repo.Missions.Single());

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("★ 9.70 from 331 ratings · Campaign of 10 missions", vm.ThiefGuildSummary);
        var saved = repo.Missions.Single();
        Assert.Equal(10, saved.CampaignMissionCount);
        Assert.Equal("A campaign.", saved.Description);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, saved.ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_ThenSave_StoresSeriesParts()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(
            null, null, "", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3, new[]
            {
                new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
                new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
            })));
        var vm = MakeViewModel(repo, lookup, seriesRepo);
        await vm.LoadSeriesOptionsAsync();
        vm.Title = "In the Lion's Den";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Dead Letter Box", "In the Lion's Den" }, seriesRepo.PartsList.OrderBy(p => p.Position).Select(p => p.Title));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionEditViewModelTests`
Expected: build error. `ThiefGuildSummary`, `Description` and the other new members don't exist yet.

- [ ] **Step 3: Implement in `MissionEditViewModel`**

Add `using System.Globalization;`. Then add these fields and members:
```csharp
    private double? _thiefGuildRating;
    private int? _thiefGuildRatingCount;
    private int? _campaignMissionCount;
    private int _thiefGuildMetadataVersion;
    private ThiefGuildSeriesInfo? _lastFetchedSeries;

    [ObservableProperty] private string? description;
    [ObservableProperty] private ThiefGuildLink? sequelOf;
    [ObservableProperty] private ThiefGuildLink? hasSequel;
    [ObservableProperty] private string? thiefGuildSummary;

    /// <summary>Sequel links only matter outside a series; the series view already orders members.</summary>
    public bool ShowSequelLinks => !HasSeriesName && (SequelOf is not null || HasSequel is not null);

    public bool HasThiefGuildInfo => ThiefGuildSummary is not null || Description is not null || ShowSequelLinks;

    partial void OnDescriptionChanged(string? value) => OnPropertyChanged(nameof(HasThiefGuildInfo));
    partial void OnThiefGuildSummaryChanged(string? value) => OnPropertyChanged(nameof(HasThiefGuildInfo));
    partial void OnSequelOfChanged(ThiefGuildLink? value) => NotifySequelVisibilityChanged();
    partial void OnHasSequelChanged(ThiefGuildLink? value) => NotifySequelVisibilityChanged();

    private void NotifySequelVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowSequelLinks));
        OnPropertyChanged(nameof(HasThiefGuildInfo));
    }

    private void RefreshThiefGuildSummary()
    {
        var parts = new List<string>();
        if (_thiefGuildRating is double rating && _thiefGuildRatingCount is int count)
            parts.Add($"★ {rating.ToString("0.00", CultureInfo.InvariantCulture)} from {count} ratings");
        if (_campaignMissionCount == 1)
            parts.Add("Single mission");
        else if (_campaignMissionCount is int missions && missions > 1)
            parts.Add($"Campaign of {missions} missions");
        ThiefGuildSummary = parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static ThiefGuildLink? LinkOrNull(string? title, string? url) =>
        string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url) ? null : new ThiefGuildLink(title, url);
```
Change the existing `partial void OnSeriesNameChanged(string? value) => OnPropertyChanged(nameof(HasSeriesName));` to:
```csharp
    partial void OnSeriesNameChanged(string? value)
    {
        OnPropertyChanged(nameof(HasSeriesName));
        NotifySequelVisibilityChanged();
    }
```
Append to `LoadFrom`:
```csharp
        _thiefGuildRating = mission.ThiefGuildRating;
        _thiefGuildRatingCount = mission.ThiefGuildRatingCount;
        _campaignMissionCount = mission.CampaignMissionCount;
        _thiefGuildMetadataVersion = mission.ThiefGuildMetadataVersion;
        Description = mission.Description;
        SequelOf = LinkOrNull(mission.SequelOfTitle, mission.SequelOfUrl);
        HasSequel = LinkOrNull(mission.HasSequelTitle, mission.HasSequelUrl);
        RefreshThiefGuildSummary();
```
In `FetchThiefGuildMetadataAsync`, after `_seriesLookupChecked = true;`, add:
```csharp
        _thiefGuildRating = result.Rating;
        _thiefGuildRatingCount = result.RatingCount;
        _campaignMissionCount = result.CampaignMissionCount;
        _thiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion;
        _lastFetchedSeries = result.Series;
        Description = result.Description;
        SequelOf = result.SequelOf;
        HasSequel = result.HasSequel;
        RefreshThiefGuildSummary();
```
In `SaveAsync`, add these to the `new FanMission { … }` initializer:
```csharp
            ThiefGuildRating = _thiefGuildRating,
            ThiefGuildRatingCount = _thiefGuildRatingCount,
            CampaignMissionCount = _campaignMissionCount,
            Description = Description,
            SequelOfTitle = SequelOf?.Title,
            SequelOfUrl = SequelOf?.Url,
            HasSequelTitle = HasSequel?.Title,
            HasSequelUrl = HasSequel?.Url,
            ThiefGuildMetadataVersion = _thiefGuildMetadataVersion,
```
Also in `SaveAsync`, just before `await _seriesRepository.DeleteOrphansAsync();`, add:
```csharp
        if (_lastFetchedSeries is not null && seriesId is int savedSeriesId)
            await SeriesAssigner.ReplacePartsIfSameSeriesAsync(savedSeriesId, _lastFetchedSeries, _seriesRepository);
```

- [ ] **Step 4: Run to verify pass**

Run the filter, then the full suite once.
Expected: all pass, 213 + 6 = 219.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/ViewModels/MissionEditViewModel.cs tests/ThiefManager.Tests/ViewModels/MissionEditViewModelTests.cs
git commit -m "Show and round-trip Thief Guild rating, type, description and sequels in Properties"
```

---

### Task 8: ThiefGuildBackfillService (rename, version-based candidates, refresh-all)

**Files:**
- Create: `src/ThiefManager/Services/ThiefGuildBackfillService.cs`, `tests/ThiefManager.Tests/Services/ThiefGuildBackfillServiceTests.cs`
- Delete: `src/ThiefManager/Services/SeriesBackfillService.cs`, `tests/ThiefManager.Tests/Services/SeriesBackfillServiceTests.cs`
- Modify: `src/ThiefManager/App.xaml.cs`, `src/ThiefManager/MainWindow.xaml.cs` (type and event renames only, to keep the build green)

**Interfaces:**
- Consumes:
  - `IMissionRepository.ApplyThiefGuildMetadataAsync(FanMission)`
  - `ThiefGuildMetadataApplier.Apply`
  - `SeriesAssigner.ApplyAsync`
  - `ThiefGuildMetadata.CurrentVersion`
- Produces:
  - `ThiefGuildBackfillService(IMissionRepository, ISeriesRepository, IThiefGuildLookupService, Func<TimeSpan, CancellationToken, Task>? delay = null)`
  - `event EventHandler? MissionUpdated`
  - `Task RunAsync(IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken, bool refreshAll = false)`

- [ ] **Step 1: Write the failing tests**

Create `tests/ThiefManager.Tests/Services/ThiefGuildBackfillServiceTests.cs`, and delete `SeriesBackfillServiceTests.cs`:
```csharp
using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildBackfillServiceTests
{
    private class UrlLookupStub : IThiefGuildLookupService
    {
        public Dictionary<string, ThiefGuildLookupResult?> Results { get; } = new();
        public List<string> FetchedUrls { get; } = new();
        public HashSet<string> Throws { get; } = new();
        public Action? OnFetch;

        public Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title) => Task.FromResult<ThiefGuildLookupResult?>(null);

        public Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url)
        {
            FetchedUrls.Add(url);
            OnFetch?.Invoke();
            if (Throws.Contains(url))
                throw new InvalidOperationException($"Bad URL: {url}");
            return Task.FromResult(Results.GetValueOrDefault(url));
        }
    }

    private class RecordingProgress : IProgress<(int Done, int Total)>
    {
        public List<(int Done, int Total)> Reports { get; } = new();
        public void Report((int Done, int Total) value) => Reports.Add(value);
    }

    private readonly FakeMissionRepository _missions = new();
    private readonly FakeSeriesRepository _series;
    private readonly UrlLookupStub _lookup = new();
    private readonly List<TimeSpan> _delays = new();

    public ThiefGuildBackfillServiceTests() => _series = new FakeSeriesRepository(_missions);

    private ThiefGuildBackfillService MakeService() =>
        new(_missions, _series, _lookup, (delay, _) => { _delays.Add(delay); return Task.CompletedTask; });

    private static ThiefGuildLookupResult Result(string url, ThiefGuildSeriesInfo? series = null, double? rating = 8.0) =>
        new(null, null, "", url, series, Rating: rating, RatingCount: rating is null ? null : 10, CampaignMissionCount: 1);

    private static ThiefGuildSeriesInfo BookSeries(int position) => new(66445, "The Book of Prophecy", position, new[]
    {
        new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
        new ThiefGuildSeriesPartInfo(2, "The Hidden City", "https://www.thiefguild.com/fanmissions/2682/p2"),
        new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
    });

    [Fact]
    public async Task RunAsync_FetchesOnlyLinkedMissionsBelowCurrentVersion()
    {
        await _missions.AddAsync(new FanMission { Title = "Old", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "NoUrl" });
        await _missions.AddAsync(new FanMission { Title = "Current", ThiefGuildUrl = "u3", ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion });
        await _missions.AddAsync(new FanMission { Title = "OldInSeries", ThiefGuildUrl = "u4", SeriesId = 5, SeriesLookupChecked = true });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(new[] { "u1", "u4" }, _lookup.FetchedUrls);
    }

    [Fact]
    public async Task RunAsync_RefreshAll_FetchesEveryLinkedMission()
    {
        await _missions.AddAsync(new FanMission { Title = "Old", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "Current", ThiefGuildUrl = "u2", ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion });
        await _missions.AddAsync(new FanMission { Title = "NoUrl" });

        await MakeService().RunAsync(null, CancellationToken.None, refreshAll: true);

        Assert.Equal(new[] { "u1", "u2" }, _lookup.FetchedUrls);
    }

    [Fact]
    public async Task RunAsync_Success_StoresMetadataStampsVersionAndRaisesMissionUpdated()
    {
        await _missions.AddAsync(new FanMission { Title = "M", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", rating: 9.02);
        var service = MakeService();
        var raised = 0;
        service.MissionUpdated += (_, _) => raised++;

        await service.RunAsync(null, CancellationToken.None);

        var mission = _missions.Missions.Single();
        Assert.Equal(9.02, mission.ThiefGuildRating);
        Assert.Equal(1, mission.CampaignMissionCount);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task RunAsync_AssignsSeriesAndStoresParts()
    {
        await _missions.AddAsync(new FanMission { Title = "Part 3", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", BookSeries(3));

        await MakeService().RunAsync(null, CancellationToken.None);

        var mission = _missions.Missions.Single();
        Assert.Equal(_series.SeriesList.Single().Id, mission.SeriesId);
        Assert.Equal(3, mission.SeriesPosition);
        Assert.Equal(3, _series.PartsList.Count);
    }

    [Fact]
    public async Task RunAsync_DoesNotRegroupAnUngroupedMission()
    {
        await _missions.AddAsync(new FanMission { Title = "Part 3", ThiefGuildUrl = "u1", SeriesLookupChecked = true });
        _lookup.Results["u1"] = Result("u1", BookSeries(3));

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Null(_missions.Missions.Single().SeriesId);
        Assert.Empty(_series.SeriesList);
    }

    [Fact]
    public async Task RunAsync_ResultWithoutSeries_KeepsStoredParts()
    {
        var series = await _series.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");
        await _series.ReplacePartsAsync(series.Id, new[] { new SeriesPart { Position = 1, Title = "Kept" } });
        await _missions.AddAsync(new FanMission { Title = "Part 1", ThiefGuildUrl = "u1", SeriesId = series.Id, SeriesPosition = 1, SeriesLookupChecked = true });
        _lookup.Results["u1"] = Result("u1", series: null);

        await MakeService().RunAsync(null, CancellationToken.None, refreshAll: true);

        Assert.Equal(series.Id, _missions.Missions.Single().SeriesId);
        Assert.Equal("Kept", _series.PartsList.Single().Title);
    }

    [Fact]
    public async Task RunAsync_LookupFails_LeavesVersionForNextLaunch()
    {
        await _missions.AddAsync(new FanMission { Title = "Offline", ThiefGuildUrl = "u1" });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(0, _missions.Missions.Single().ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task RunAsync_LookupThrows_SkipsMissionAndContinues()
    {
        await _missions.AddAsync(new FanMission { Title = "Broken", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "Fine", ThiefGuildUrl = "u2" });
        _lookup.Throws.Add("u1");
        _lookup.Results["u2"] = Result("u2");

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(0, _missions.Missions.Single(m => m.Title == "Broken").ThiefGuildMetadataVersion);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, _missions.Missions.Single(m => m.Title == "Fine").ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task RunAsync_WaitsOneSecondBetweenRequests_AndReportsProgress()
    {
        await _missions.AddAsync(new FanMission { Title = "A", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "B", ThiefGuildUrl = "u2" });
        await _missions.AddAsync(new FanMission { Title = "C", ThiefGuildUrl = "u3" });
        var progress = new RecordingProgress();

        await MakeService().RunAsync(progress, CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) }, _delays);
        Assert.Equal(new[] { (0, 3), (1, 3), (2, 3), (3, 3) }, progress.Reports);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsFetching()
    {
        await _missions.AddAsync(new FanMission { Title = "A", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "B", ThiefGuildUrl = "u2" });
        using var cts = new CancellationTokenSource();
        _lookup.OnFetch = cts.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MakeService().RunAsync(null, cts.Token));

        Assert.Equal(new[] { "u1" }, _lookup.FetchedUrls);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter ThiefGuildBackfillServiceTests`
Expected: build error, because `ThiefGuildBackfillService` doesn't exist yet.

- [ ] **Step 3: Implement**

Delete `SeriesBackfillService.cs` and create `src/ThiefManager/Services/ThiefGuildBackfillService.cs`:
```csharp
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Services;

/// <summary>
/// Fetches Thief Guild data for linked missions: at startup only for missions last fetched by an
/// older parser (below ThiefGuildMetadata.CurrentVersion), or for every linked mission when the
/// user asks to refresh. A successful fetch stamps the current version; a failed one leaves it so
/// the mission is retried next launch. Requests are sequential and spaced out to go easy on the site.
/// </summary>
public class ThiefGuildBackfillService
{
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(1);

    private readonly IMissionRepository _missionRepository;
    private readonly ISeriesRepository _seriesRepository;
    private readonly IThiefGuildLookupService _lookupService;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ThiefGuildBackfillService(
        IMissionRepository missionRepository,
        ISeriesRepository seriesRepository,
        IThiefGuildLookupService lookupService,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _missionRepository = missionRepository;
        _seriesRepository = seriesRepository;
        _lookupService = lookupService;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Raised after each mission whose fetched data was saved.</summary>
    public event EventHandler? MissionUpdated;

    public async Task RunAsync(IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken, bool refreshAll = false)
    {
        var candidates = (await _missionRepository.GetAllAsync())
            .Where(m => !string.IsNullOrWhiteSpace(m.ThiefGuildUrl)
                && (refreshAll || m.ThiefGuildMetadataVersion < ThiefGuildMetadata.CurrentVersion))
            .ToList();
        if (candidates.Count == 0)
            return;

        progress?.Report((0, candidates.Count));
        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (i > 0)
                await _delay(DelayBetweenRequests, cancellationToken);

            var mission = candidates[i];
            try
            {
                var result = await _lookupService.FetchByUrlAsync(mission.ThiefGuildUrl!);
                if (result is not null)
                {
                    ThiefGuildMetadataApplier.Apply(mission, result);
                    await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
                    // Fetch-owned columns only: `mission` was loaded before the loop and may be stale.
                    await _missionRepository.ApplyThiefGuildMetadataAsync(mission);
                    MissionUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A malformed URL or unexpected failure for this mission shouldn't stop the rest;
                // its version is left as-is, so it's retried on the next launch.
            }

            progress?.Report((i + 1, candidates.Count));
        }

        // A series created for a mission deleted mid-run would otherwise linger.
        await _seriesRepository.DeleteOrphansAsync();
    }
}
```
Keep the build green with the minimum renames. (Task 9 reworks this code.)
- **App.xaml.cs:** `new SeriesBackfillService(` becomes `new ThiefGuildBackfillService(`, and rename the variable to `thiefGuildBackfillService`.
- **MainWindow.xaml.cs:**
  - The constructor parameter type and the field type change from `SeriesBackfillService` to `ThiefGuildBackfillService`.
  - `_seriesBackfillService.SeriesAssigned += SeriesBackfill_SeriesAssigned;` becomes `_seriesBackfillService.MissionUpdated += SeriesBackfill_SeriesAssigned;`. Change the matching `-=` line the same way.

- [ ] **Step 4: Run to verify pass**

Run: `dotnet build src/ThiefManager`, the filter, then the full suite once.
Expected: build clean; all pass. That's 219 − 7 (the old backfill tests) + 10 = 222.

- [ ] **Step 5: Commit**

```bash
git add -A src/ThiefManager/Services src/ThiefManager/App.xaml.cs src/ThiefManager/MainWindow.xaml.cs tests/ThiefManager.Tests/Services
git commit -m "Replace SeriesBackfillService with ThiefGuildBackfillService using metadata versions"
```

---

### Task 9: Wire up the UI

**Files:**
- Modify:
  - `src/ThiefManager/Views/MissionListRowTemplateSelector.cs`
  - `src/ThiefManager/MainWindow.xaml`, `src/ThiefManager/MainWindow.xaml.cs`
  - `src/ThiefManager/Views/MissionEditWindow.xaml`, `src/ThiefManager/Views/MissionEditWindow.xaml.cs`
  - `src/ThiefManager/App.xaml.cs` (variable name only, if Task 8 didn't already rename it)

**Interfaces:**
- Consumes:
  - `MissingPartRow`, `MissionRow.ThiefGuildRatingDisplay` and `MissionRow.MissionTypeDisplay` (Task 5)
  - The Task 6 view-model members
  - `ThiefGuildBackfillService` (Task 8)
  - The Task 7 edit view-model members

This task is XAML and code-behind that the xUnit project can't exercise. It's verified by build, the full test suite and careful reading of the XAML. **Do not launch the app.**

- [ ] **Step 1: Template selector: add a missing-part template**

In `MissionListRowTemplateSelector`, add `public DataTemplate? MissingPartTemplate { get; set; }`, and add `MissingPartRow => MissingPartTemplate ?? EmptyTemplate,` to the switch (before the `_` arm).

- [ ] **Step 2: Main list columns**

In `MainWindow.xaml`:
- In the Title column's `views:MissionListRowTemplateSelector`, add:
```xml
                <views:MissionListRowTemplateSelector.MissingPartTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding DisplayTitle}" FontStyle="Italic" Margin="22,0,0,0"
                                   Foreground="{StaticResource MutedForegroundBrush}" TextTrimming="CharacterEllipsis"/>
                    </DataTemplate>
                </views:MissionListRowTemplateSelector.MissingPartTemplate>
```
- Insert two columns directly after the `RatingColumn` `GridViewColumn`:
```xml
    <GridViewColumn x:Name="ThiefGuildRatingColumn" Header="TG Rating" Width="110">
        <GridViewColumn.CellTemplateSelector>
            <views:MissionListRowTemplateSelector>
                <views:MissionListRowTemplateSelector.MissionTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding ThiefGuildRatingDisplay}" VerticalAlignment="Center"/>
                    </DataTemplate>
                </views:MissionListRowTemplateSelector.MissionTemplate>
            </views:MissionListRowTemplateSelector>
        </GridViewColumn.CellTemplateSelector>
    </GridViewColumn>
    <GridViewColumn x:Name="MissionTypeColumn" Header="Type" Width="110">
        <GridViewColumn.CellTemplateSelector>
            <views:MissionListRowTemplateSelector>
                <views:MissionListRowTemplateSelector.MissionTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding MissionTypeDisplay}" VerticalAlignment="Center"/>
                    </DataTemplate>
                </views:MissionListRowTemplateSelector.MissionTemplate>
            </views:MissionListRowTemplateSelector>
        </GridViewColumn.CellTemplateSelector>
    </GridViewColumn>
```
- In `MainWindow.xaml.cs`, add `[ThiefGuildRatingColumn] = SortField.ThiefGuildRating,` and `[MissionTypeColumn] = SortField.MissionType,` to the `_sortableColumns` initializer.

- [ ] **Step 3: Context menu**

In the ListView's `ContextMenu`:
- For every mission-only `MenuItem` and `Separator` (Properties…, Series…, Set Status, both separators, Install, Uninstall…, Delete), change
  `Visibility="{Binding IsSeriesHeaderSelected, Converter={StaticResource InverseBoolToVisibilityConverter}}"`
  to
  `Visibility="{Binding ShowMissionMenuItems, Converter={StaticResource BoolToVisibilityConverter}}"`.
- After "Ungroup Series...", add:
```xml
                    <MenuItem Header="Open Series on Thief Guild" Click="OpenSeriesOnThiefGuild_Click"
                              Visibility="{Binding CanOpenSelectedSeriesOnThiefGuild, Converter={StaticResource BoolToVisibilityConverter}}">
                        <MenuItem.Icon>
                            <ui:SymbolIcon Symbol="Globe24" Foreground="#FF64B5F6"/>
                        </MenuItem.Icon>
                    </MenuItem>
                    <MenuItem Header="Open on Thief Guild" Click="OpenOnThiefGuild_Click"
                              Visibility="{Binding CanOpenSelectedOnThiefGuild, Converter={StaticResource BoolToVisibilityConverter}}">
                        <MenuItem.Icon>
                            <ui:SymbolIcon Symbol="Globe24" Foreground="#FF64B5F6"/>
                        </MenuItem.Icon>
                    </MenuItem>
```

- [ ] **Step 4: Refresh menu item**

In the top `Menu`, after the "_Ignore List..." item, add:
```xml
            <MenuItem Header="_Refresh Thief Guild Data..." Style="{StaticResource CompactTopLevelMenuItemStyle}" Click="RefreshThiefGuildData_Click"
                      IsEnabled="{Binding CanRefreshThiefGuild}"
                      ToolTip="Re-fetch rating, type, description and series info for every mission linked to Thief Guild.">
                <MenuItem.Icon>
                    <ui:SymbolIcon Symbol="ArrowClockwise24"/>
                </MenuItem.Icon>
            </MenuItem>
```
(If `ArrowClockwise24` isn't in WPF-UI 4.3's `SymbolRegular`, use `ArrowSync24`. Verify which one exists before choosing.)

- [ ] **Step 5: Code-behind (`MainWindow.xaml.cs`)**

- Rename the field `_seriesBackfillService` to `_thiefGuildBackfillService` everywhere.
- Add `private DateTime _lastBackfillReload = DateTime.MinValue;`.
- Replace `RunSeriesBackfillAsync` and `SeriesBackfill_SeriesAssigned` with:
```csharp
    private async Task RunThiefGuildBackfillAsync(bool refreshAll)
    {
        if (_viewModel.IsThiefGuildRefreshRunning)
            return;

        _viewModel.IsThiefGuildRefreshRunning = true;
        var label = refreshAll ? "Refreshing Thief Guild data…" : "Updating Thief Guild data…";
        var progress = new Progress<(int Done, int Total)>(p =>
            _viewModel.BackgroundStatus = p.Done < p.Total ? $"{label} {p.Done}/{p.Total}" : null);

        _thiefGuildBackfillService.MissionUpdated += ThiefGuildBackfill_MissionUpdated;
        try
        {
            await _thiefGuildBackfillService.RunAsync(progress, _backfillCancellation.Token, refreshAll);
            // Reload so in-memory missions carry the fetched data; otherwise a later whole-row save
            // from a stale copy could write old values back.
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // Best-effort: cancellation on close or a database hiccup just leaves the remaining
            // missions at their old version, and they're retried on the next launch.
        }
        finally
        {
            _thiefGuildBackfillService.MissionUpdated -= ThiefGuildBackfill_MissionUpdated;
            _viewModel.BackgroundStatus = null;
            _viewModel.IsThiefGuildRefreshRunning = false;
        }
    }

    private async void ThiefGuildBackfill_MissionUpdated(object? sender, EventArgs e)
    {
        // Rebuilding the list resets its scroll and focus, so while a run is fetching a mission per
        // second, refresh at most every few seconds; the run's final reload catches up the rest.
        if (DateTime.UtcNow - _lastBackfillReload < TimeSpan.FromSeconds(5))
            return;
        _lastBackfillReload = DateTime.UtcNow;

        try
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception)
        {
            // A failed mid-run refresh is harmless: the reload when the run finishes catches up.
        }
    }

    private async void RefreshThiefGuildData_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsThiefGuildRefreshRunning)
            return;

        var linkedCount = (await _missionRepository.GetAllAsync()).Count(m => !string.IsNullOrWhiteSpace(m.ThiefGuildUrl));
        if (linkedCount == 0)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = "Refresh Thief Guild Data",
                Content = "No missions are linked to Thief Guild yet.",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
            return;
        }

        var confirm = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = "Refresh Thief Guild Data",
            Content = $"Re-fetch Thief Guild data for {linkedCount} linked mission(s)?\n\nThis takes about {linkedCount} second(s) and runs in the background.",
            PrimaryButtonText = "Refresh",
            CloseButtonText = "Cancel"
        };

        if (await confirm.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
            await RunThiefGuildBackfillAsync(refreshAll: true);
    }

    private void OpenOnThiefGuild_Click(object sender, RoutedEventArgs e) => OpenInBrowser(_viewModel.SelectedThiefGuildUrl);

    private void OpenSeriesOnThiefGuild_Click(object sender, RoutedEventArgs e) => OpenInBrowser(_viewModel.SelectedSeriesThiefGuildUrl);

    private static void OpenInBrowser(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser registered or a malformed stored URL; nothing useful to do.
        }
    }
```
- In the constructor's `Loaded` handler, change `await RunSeriesBackfillAsync();` to `await RunThiefGuildBackfillAsync(refreshAll: false);`.
- In `MissionList_DoubleClick`, after the chevron guard, add:
```csharp
        if (_viewModel.SelectedRow is MissingPartRow missingPart)
        {
            OpenInBrowser(missingPart.Part.ThiefGuildUrl);
            return;
        }
```

- [ ] **Step 6: Properties window**

In `MissionEditWindow.xaml`, insert this block directly **above** the `StackPanel` that holds the Globe icon and the "Thief Guild URL" label:
```xml
            <StackPanel Visibility="{Binding HasThiefGuildInfo, Converter={StaticResource BoolToVisibilityConverter}}" Margin="0,0,0,10">
                <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                    <ui:SymbolIcon Symbol="Star24" Foreground="#FFFFD54F" Margin="0,0,6,0"/>
                    <TextBlock Text="Thief Guild"/>
                </StackPanel>
                <TextBlock Text="{Binding ThiefGuildSummary}" Foreground="{StaticResource MutedForegroundBrush}" Margin="0,0,0,4"
                           Visibility="{Binding ThiefGuildSummary, Converter={StaticResource NullToVisibilityConverter}}"/>
                <TextBox Text="{Binding Description, Mode=OneWay}" IsReadOnly="True" TextWrapping="Wrap"
                         VerticalScrollBarVisibility="Auto" MaxHeight="100" Margin="0,0,0,4"
                         Visibility="{Binding Description, Converter={StaticResource NullToVisibilityConverter}}"/>
                <StackPanel Visibility="{Binding ShowSequelLinks, Converter={StaticResource BoolToVisibilityConverter}}">
                    <TextBlock Visibility="{Binding SequelOf, Converter={StaticResource NullToVisibilityConverter}}">
                        <Run Text="Sequel of: "/><Hyperlink NavigateUri="{Binding SequelOf.Url}" RequestNavigate="Hyperlink_RequestNavigate"><Run Text="{Binding SequelOf.Title, Mode=OneWay}"/></Hyperlink>
                    </TextBlock>
                    <TextBlock Visibility="{Binding HasSequel, Converter={StaticResource NullToVisibilityConverter}}">
                        <Run Text="Has a sequel: "/><Hyperlink NavigateUri="{Binding HasSequel.Url}" RequestNavigate="Hyperlink_RequestNavigate"><Run Text="{Binding HasSequel.Title, Mode=OneWay}"/></Hyperlink>
                    </TextBlock>
                </StackPanel>
            </StackPanel>
```
In `MissionEditWindow.xaml.cs`, add `using System.Diagnostics;` and `using System.Windows.Navigation;` if they're missing, then add:
```csharp
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser registered; nothing useful to do.
        }
        e.Handled = true;
    }
```
Change the Thief Guild URL help text to: "Paste this mission's Thief Guild page to fetch its author, release year, tags, series, rating and description. Leave blank and click Fetch to search by title instead."

- [ ] **Step 7: Build, test, check the XAML carefully**

Run: `dotnet build src/ThiefManager` (0 errors, 0 warnings), then `dotnet test tests/ThiefManager.Tests`. Expected: 222 passing.

Because the app can't be launched, check by reading:
- Every `StaticResource` key used exists in `App.xaml` or `DarkTheme.xaml`: `BoolToVisibilityConverter`, `NullToVisibilityConverter`, `MutedForegroundBrush`, `CompactTopLevelMenuItemStyle`.
- Every new binding path exists on the bound type:
  - row types for cell templates
  - `MainViewModel` for the menus (the ContextMenu's DataContext is the view model)
  - `MissionEditViewModel` for the Properties section
- `ArrowClockwise24` exists in `SymbolRegular`, or `ArrowSync24` is used instead.

- [ ] **Step 8: Commit**

```bash
git add src/ThiefManager/Views src/ThiefManager/MainWindow.xaml src/ThiefManager/MainWindow.xaml.cs src/ThiefManager/App.xaml.cs
git commit -m "Show Thief Guild rating, type and missing series parts, and add Refresh Thief Guild Data"
```

---

### Task 10: Version bump and changelog

**Files:**
- Modify: `src/ThiefManager/AppVersion.cs`, `src/ThiefManager/ChangelogEntry.cs`

- [ ] **Step 1: Bump the version:** `AppVersion.Current = "3.9.3";`

- [ ] **Step 2: Add a changelog entry** at the top of `ChangelogData.Entries`:
```csharp
        new("3.9.3", "2026-09-25", new[]
        {
            "Added TG Rating and Type columns to the mission list, showing each mission's Thief Guild community rating (e.g. ★ 9.02 (229)) and whether it's a campaign; both are sortable.",
            "Mission Properties now show the Thief Guild rating, mission type and description, plus \"Sequel of\" / \"Has a sequel\" links for missions that aren't part of a series.",
            "Series now list the parts you don't own as dimmed placeholders (e.g. \"#1 · Dead Letter Box — not in library\"), with the header showing how many you own; double-click one to open its Thief Guild page.",
            "Added Refresh Thief Guild Data to re-fetch ratings and details for every linked mission. Missions already linked to Thief Guild are updated once automatically in the background."
        }),
```

- [ ] **Step 3: Build and test**

Run: `dotnet build src/ThiefManager` and `dotnet test tests/ThiefManager.Tests`.
Expected: build clean and 222 passing.

- [ ] **Step 4: Commit**

```bash
git add src/ThiefManager/AppVersion.cs src/ThiefManager/ChangelogEntry.cs
git commit -m "Bump to 3.9.3: richer Thief Guild metadata"
```
