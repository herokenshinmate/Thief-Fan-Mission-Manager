# FM Series — Design

**Date:** 2026-09-25
**Status:** Draft for review

## Goal

Some fan missions are released as separate single FMs that form a series (e.g. *The Book of Prophecy* Parts 1–3), as opposed to pre-packed campaigns like *The Black Parade*, which stay a single mission entry. This feature lets missions be linked to a series with an order, groups series members together in the main mission list under a collapsible header, and pulls series information from Thief Guild automatically.

### Success criteria

- A mission can belong to at most one series and has a position within it.
- The main list shows each series as a collapsible header row, sorted alongside standalone missions, with its members listed underneath in series order.
- Thief Guild lookups (install-time, Properties, and a one-time startup backfill of the existing library) fill in the series and position without overwriting anything the user set manually.
- The series and position can be set, changed or cleared by hand in Properties.

### Out of scope

- Pre-packed campaigns (one archive with several missions) are not split up.
- No series-level data beyond name, Thief Guild id and expanded state (no notes or description).
- Series members the user doesn't own are not shown as placeholders.

## Thief Guild source data

Each mission detail page (e.g. `/fanmissions/66450/...`) has an `<h6>` in its header containing:

```html
<a href="/fanmissions?series=66445">The Book of Prophecy:</a>
<br/>
<a title="The Book of Prophecy Part 1: Dead Letter Box (2007)" class="text-muted" href="/fanmissions/2684/...">TBOPP1DLB</a>
<a title="The Book of Prophecy Part 2: The Hidden City (2009)" class="text-muted" href="/fanmissions/2682/...">TBOPP2THC</a>
TBOPP3ITLD          <!-- the current mission: plain text, not a link -->
```

The members are listed in series order, and the current mission is the one unlinked abbreviation. The page also has a `Series: <a href="/fanmissions?series=66445">The Book of Prophecy</a>` list item and a "Sequel of:" item. These aren't needed.

Search result cards (`div.panel` on `/fanmissions/?search=...`) do **not** include the series block. Only the detail page has it.

## Data model

### New entity `Series` (table `Series`)

| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `Name` | TEXT NOT NULL | Display name |
| `ThiefGuildSeriesId` | INTEGER NULL | Unique index where not null |
| `IsExpanded` | INTEGER NOT NULL DEFAULT 1 | Persisted header expand state |

Created for existing databases by `SchemaUpgrader.CreateTableIfMissing` (the same pattern as `IgnoredFms`), including `CREATE UNIQUE INDEX ... ON Series(ThiefGuildSeriesId) WHERE ThiefGuildSeriesId IS NOT NULL`. Added as `DbSet<Series>` on `ThiefManagerDbContext` so `EnsureCreated` builds it for new databases, with the unique filtered index configured in `OnModelCreating`.

### `FanMission` additions

| Property | Type | Upgrade column definition |
|---|---|---|
| `SeriesId` | `int?` | `INTEGER NULL` |
| `SeriesPosition` | `int?` | `INTEGER NULL` |
| `SeriesLookupChecked` | `bool` | `INTEGER NOT NULL DEFAULT 0` |

`SeriesId` is a plain nullable foreign-key value with no EF navigation property. That keeps `MissionRepository` queries and the existing fakes unchanged. The series is resolved in memory from `ISeriesRepository.GetAllAsync()`.

**Position semantics:** `SeriesPosition` is the mission's absolute position in the series as Thief Guild orders it. Owning only Parts 2 and 3 yields positions 2 and 3, and the gap is shown as it is.

### `ISeriesRepository` / `SeriesRepository`

- `Task<List<Series>> GetAllAsync()`
- `Task<Series> GetOrCreateByThiefGuildIdAsync(int thiefGuildSeriesId, string name)`: returns the existing row, updating `Name` if it differs, or creates one.
- `Task<Series> GetOrCreateByNameAsync(string name)`: trims the name and matches case-insensitively, including rows that have a Thief Guild id. Creates the row if there's no match.
- `Task RenameAsync(int id, string name)`
- `Task SetExpandedAsync(int id, bool isExpanded)`
- `Task DeleteAsync(int id)`: clears `SeriesId`/`SeriesPosition` on its members, then deletes the row.
- `Task DeleteOrphansAsync()`: deletes series rows that no mission references.

`tests/ThiefManager.Tests/Fakes/FakeSeriesRepository.cs` is an in-memory implementation for tests. Its orphan detection works from a mission list supplied by the test.

## Scraper

### Result shape

```csharp
public record ThiefGuildSeriesInfo(int ThiefGuildSeriesId, string Name, int Position);
public record ThiefGuildLookupResult(string? Author, int? ReleaseYear, string Tags, string Url, ThiefGuildSeriesInfo? Series = null);
```

### `ThiefGuildPageParser.ExtractSeries(IParentNode scope)`

1. Find the first `h6 a[href*='?series=']`. If there is none, return `null`.
2. Parse the series id from the `series=` query value (int). If parsing fails, return `null`.
3. Name = link text trimmed, with one trailing `:` removed. If the name is empty, return `null`.
4. Walk the child nodes of that `h6` that come after the `<br>` following the series link, in document order. Each `<a href="/fanmissions/...">` element counts as one member. Each non-whitespace text node counts as one member, and a text node marks the current mission.
5. Position = 1-based index of the current (unlinked) entry. If there isn't exactly one unlinked entry, return `null`.

`BuildResult` calls `ExtractSeries` and sets `Series`. The parser never throws on unexpected markup; it returns `null`.

### `ThiefGuildLookupService`

- **Detail-page paths** (`FetchByUrlAsync`, and the single-match redirect in `SearchByTitleAsync`) already parse a detail page, so series info comes back directly.
- **Search-card match path:** after choosing the matching card, fetch that card's detail URL and build the result from the detail page. This keeps everything the detail page provides, series included. If that second fetch fails, fall back to the card-based result without series.

## Applying series info

A shared helper, `SeriesAssigner.ApplyAsync(FanMission mission, ThiefGuildSeriesInfo? info, ISeriesRepository repo)`:

- If `mission.SeriesId` is already set, it does nothing to the series fields, so a manual assignment always wins.
- Otherwise, if `info` isn't null, it calls `GetOrCreateByThiefGuildIdAsync(info.ThiefGuildSeriesId, info.Name)`, then sets `SeriesId` and `SeriesPosition = info.Position`.
- In both cases it sets `SeriesLookupChecked = true`.

`MainViewModel.ApplyThiefGuildMetadataAsync` (install-time lookup) and `MissionEditViewModel`'s Thief Guild lookup both use this helper. In `MissionEditViewModel`, the looked-up series only pre-fills the Series/Position fields when they are blank. It's saved with the rest of the form.

## Startup backfill: `SeriesBackfillService`

- **Constructor:** `IMissionRepository`, `ISeriesRepository`, `IThiefGuildLookupService`, and an injectable delay function (so tests run instantly).
- **`Task RunAsync(IProgress<(int Done, int Total)> progress, CancellationToken ct)`:**
  - **Candidates:** missions where `ThiefGuildUrl` is set, `SeriesId` is null, and `SeriesLookupChecked` is false.
  - **Fetching:** one at a time, with a 1-second delay between requests, calling `FetchByUrlAsync(url)`.
  - **Non-null result:** runs `SeriesAssigner.ApplyAsync`, saves the mission, and raises an `MissionUpdated` event (or callback) so the main list re-groups.
  - **Null result (network failure or bad page):** leaves the flag false so the mission is retried on the next launch.
  - **Scope of changes:** only the series fields and `SeriesLookupChecked` are written. Author, tags and other metadata are left untouched.
- **Startup:** `MainWindow` starts it after the initial `LoadCommand` completes, with a `CancellationToken` cancelled on window close. While it runs, the status bar shows `Checking Thief Guild for series info… {done}/{total}`, and the text clears when it finishes. Cancellation and exceptions are caught and swallowed, so the backfill never breaks the app.

## Main list UI

### Row model

```csharp
public abstract class MissionListRow : ObservableObject { }
public sealed class MissionRow : MissionListRow { FanMission Mission; bool IsSeriesMember; }
public sealed class SeriesHeaderRow : MissionListRow
{
    Series Series; int ShownCount; int TotalCount; bool IsExpanded;
    string? CommonGameDisplay;   // set when all members share a game
    int CompletedCount;          // among all owned members
}
```

`MainViewModel.VisibleMissions` (an `ObservableCollection<FanMission>`) is replaced by `VisibleRows` (an `ObservableCollection<MissionListRow>`).

### `MissionListBuilder.Build(...)`

This is a pure static function, unit-tested like `MissionQuery`:

```csharp
IReadOnlyList<MissionListRow> Build(
    IReadOnlyList<FanMission> filteredSorted,   // output of MissionQuery.Apply
    IReadOnlyList<FanMission> allMissions,      // for TotalCount / CompletedCount
    IReadOnlyList<Series> series,
    SortField sortField, bool ascending)
```

Rules:

1. Missions whose `SeriesId` has no matching `Series` are treated as standalone.
2. **Grouping:** filtered missions are grouped by `SeriesId`. A series appears only if at least one member passed the filters. `ShownCount` counts the filtered members, and `TotalCount` counts all members in the library.
3. **Placement:** standalone missions and series groups are ordered together as units.
   - For `SortField.Title`, a group's sort key is the series name.
   - For every other field, the key is that field's value on the group's first member in `filteredSorted` order. This reuses `MissionQuery`'s key selection, pulled out into a shared `MissionQuery.SortKey(mission, sortField)` helper.
   - Ties keep the order the units had in `filteredSorted`.
4. **Members:** ordered by `SeriesPosition` ascending (null last), then by `Title`, whatever the sort. They're emitted after their header only when `IsExpanded` is true.

### Rendering (GridView cells)

| Column | `SeriesHeaderRow` | `MissionRow` (member) | `MissionRow` (standalone) |
|---|---|---|---|
| Title | chevron (▶/▼) + name + `(N)` or `(shown of N shown)` | indented, `#pos · Title` (no `#` prefix when position is null) | unchanged |
| Game | `CommonGameDisplay` with icon, else blank | unchanged | unchanged |
| Status | `completed/total completed` | unchanged | unchanged |
| Others | blank | unchanged | unchanged |

Each column's `CellTemplate` switches on the row type through a `DataTemplateSelector` (or `DataTrigger`s on a type-check converter). The header row gets a slightly bolder or tinted style from `DarkTheme.xaml`.

### Interaction

- **Expanding and collapsing:** clicking the chevron, or double-clicking the header, toggles `IsExpanded`. The change is persisted with `SetExpandedAsync`, then the rows are rebuilt.
- **Selection:** the ListView binds `SelectedItem` to `SelectedRow`. `SelectedMission` becomes a derived value: the mission for a `MissionRow`, `null` for a header. The existing commands keep using `SelectedMission`, so Play, Delete, Set Status, Install and Uninstall are disabled on headers. After a rebuild, the previously selected mission (by `Id`) or series header (by `Series.Id`) is re-selected.
- **Header context menu:** Rename Series… (a small text prompt), Expand/Collapse, and Ungroup Series (`ISeriesRepository.DeleteAsync`).
- **Mission context menu:** gains "Series…", which opens Properties.
- **Deleting missions:** deleting a mission calls `DeleteOrphansAsync()`.

## Properties window (`MissionEditViewModel` / its view)

- New fields: `SeriesName` (editable `ComboBox` bound to the existing series names, blank = none) and `SeriesPosition` (`int?` number box, enabled only when a series name is entered).
- **Saving:**
  - A blank name clears `SeriesId` and `SeriesPosition`.
  - Otherwise the name goes through `GetOrCreateByNameAsync`, and `SeriesId` and `SeriesPosition` are set. Choosing the name of a Thief Guild-linked series joins that series.
  - Then `DeleteOrphansAsync()` runs.
  - If the user changed the series fields by hand, `SeriesLookupChecked` is set to true so the backfill never touches the mission.

## Error handling summary

- Parser: returns `null` for missing or unrecognized markup and never throws.
- Network: the existing `HttpRequestException`/`TaskCanceledException` handling returns `null`. The backfill leaves the mission to retry next launch.
- Concurrency: series creation comes from the backfill (sequential) and user actions on the UI thread. The unique index on `ThiefGuildSeriesId` backs up `GetOrCreate`.
- Orphans: cleaned up after mission deletes and Properties saves.

## Testing

- **`ThiefGuildPageParserTests`:** trimmed fixtures of the real Part 2 and Part 3 pages give the series id 66445, the name "The Book of Prophecy", and positions 2 and 3. A page with no series gives `null`. A page with two unlinked entries, and one with a non-numeric series id, also give `null`.
- **`ThiefGuildLookupService`:** the search-card path fetches the detail page. Where the existing tests can't reach HTTP, this is covered by the parser tests plus manual verification.
- **`MissionListBuilderTests`:**
  - standalone-only lists come out unchanged
  - grouping and member order, including gaps and null positions
  - shown/total counts under filtering
  - series hidden when no member matches
  - group placement for Title sort and for a non-title sort, ascending and descending
  - collapsed series emit only the header
  - an unknown `SeriesId` is treated as standalone
  - `CommonGameDisplay` and `CompletedCount` are correct
- **`SeriesAssignerTests`:** a manual series is kept, a new series is created from info, an existing TG series is reused and renamed, and the flag is set.
- **`SeriesBackfillServiceTests`:** candidate selection is right, the flag is set on success and not on a null result, cancellation stops the loop, and progress is reported.
- **`MainViewModelTests`:** `SelectedRow` maps to `SelectedMission`, commands are disabled on headers, expand/collapse persists, rename and ungroup work, and orphans are cleaned up after delete.
- **`MissionEditViewModelTests`:** saving with a new name, an existing name, and a blank name; the lookup pre-fills blank series fields only.

## Release

- Bump the patch version in `AppVersion.cs`, following the patch-per-change convention.
- Add a `ChangelogEntry` describing series grouping, Thief Guild series detection, and the one-time backfill.
