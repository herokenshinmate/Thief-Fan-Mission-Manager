# Richer Thief Guild Metadata — Design

**Date:** 2026-09-25
**Status:** Draft for review
**Builds on:** `2026-09-25-fm-series-design.md` (shipped in 3.9.1–3.9.2)

## Goal

Capture more of what a mission's Thief Guild detail page already shows, from the same single page fetch the app already makes:

1. the community **rating** and number of ratings;
2. the **mission type** (single mission or campaign of N missions);
3. the mission's **description**;
4. the **complete part list** of a series, so missions you don't own show up as placeholders, plus "sequel of" / "has a sequel" links for missions that aren't in a series.

### Success criteria

- The main list has sortable **TG Rating** and **Type** columns.
- Properties shows the rating, type, description and (for non-series missions) sequel links.
- An expanded series with a known part list shows dimmed placeholder rows for the parts you don't own, and a "(2 of 3 owned)" header. Double-clicking a placeholder opens its Thief Guild page.
- The existing library gets the new fields automatically, once, through the startup backfill.
- A **Refresh Thief Guild Data** menu item re-fetches every linked mission on demand. It's the only way ratings get updated later.
- Nothing you entered yourself is overwritten, and your own 0–5 Rating is untouched.

### Out of scope

- Periodic or automatic rating refresh. The user chose manual refresh.
- A toolbar filter for Type. Sorting by Type is enough for now.
- Downloading or installing missing series parts. Placeholders only link to Thief Guild.
- Difficulty, length and language. Thief Guild doesn't provide these as structured data.

## Source markup (verified against Endless Rain, The Black Parade, and Book of Prophecy Part 3)

| Field | Markup | Example |
|---|---|---|
| Rating | `<p>` containing `<i class="material-icons …">star</i>`, whose text is `9.<small>02</small>`, so `TextContent` gives "star 9.02" | 9.02 |
| Rating count | `<a href="/fanmissions/rating_list/{id}" …>229 ratings</a>` | 229 |
| Type | a sidebar `<li class="list-group-item">` whose text is "Single mission" or "Campaign of 10 missions" | 1 / 10 |
| Description | `<script type="application/ld+json">`, a JSON object with a `description` string (escaped, with CRLF newlines) | text |
| Sequel of | `<li class="list-group-item">Sequel of:<br/><a href="/works/{guid}">Title</a></li>` | title + URL |
| Has a sequel | `<li class="list-group-item text-muted"><small>FM has a sequel:<br/><a href="/works/{guid}">Title</a></small></li>` | title + URL |
| Series parts | the existing header `<h6>` series block. Each other member is `<a title="{Full Title} ({year})" href="/fanmissions/{id}/{slug}">ABBR</a>`, and the current mission is unlinked text | ordered list |

`/works/{guid}` URLs redirect (302) to `/fanmissions/{id}`. They're stored as absolute `https://www.thiefguild.com/works/{guid}` and opened as-is.

## Data model

### `FanMission` additions

| Property | Type | Upgrade column definition |
|---|---|---|
| `ThiefGuildRating` | `double?` | `REAL NULL` |
| `ThiefGuildRatingCount` | `int?` | `INTEGER NULL` |
| `CampaignMissionCount` | `int?` | `INTEGER NULL` (1 = single mission; null = unknown) |
| `Description` | `string?` | `TEXT NULL` |
| `SequelOfTitle`, `SequelOfUrl` | `string?` | `TEXT NULL` |
| `HasSequelTitle`, `HasSequelUrl` | `string?` | `TEXT NULL` |
| `ThiefGuildMetadataVersion` | `int` | `INTEGER NOT NULL DEFAULT 0` |

A new constant, `ThiefGuildMetadata.CurrentVersion = 2`, marks data fetched by this version's parser. Missions fetched by 3.9.1 are version 0.

`SeriesLookupChecked` keeps one narrower meaning: **automatic series assignment has already been decided for this mission.** It's still set by the first lookup and by Ungroup. Automatic assignment now requires `SeriesId == null && !SeriesLookupChecked`. That way a refresh can never regroup a series you ungrouped, and a mission already checked isn't assigned later.

### New entity `SeriesPart` (table `SeriesParts`)

| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AUTOINCREMENT | |
| `SeriesId` | INTEGER NOT NULL | the owning `Series.Id` (no navigation property, same as `FanMission.SeriesId`) |
| `Position` | INTEGER NOT NULL | 1-based |
| `Title` | TEXT NOT NULL | from the link's `title` attribute with a trailing " (yyyy)" removed. For the current mission, that mission's own title |
| `ThiefGuildUrl` | TEXT NULL | absolute URL. Null for the current mission's entry, which has no link |

The table is created by `SchemaUpgrader.CreateTableIfMissing` and added as a `DbSet` on the context. It has an index on `SeriesId`.

`ISeriesRepository` gains:
- `GetAllPartsAsync()`
- `ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)`, which deletes the series' existing parts and inserts the new list in one `SaveChanges`

`DeleteAsync(seriesId)` and `DeleteOrphansAsync()` also remove the affected series' parts. The fake repository mirrors all of this.

## Scraper

`ThiefGuildLookupResult` gains the new fields, all optional:

```csharp
public record ThiefGuildLookupResult(
    string? Author, int? ReleaseYear, string Tags, string Url,
    ThiefGuildSeriesInfo? Series = null,
    double? Rating = null, int? RatingCount = null, int? CampaignMissionCount = null,
    string? Description = null,
    ThiefGuildLink? SequelOf = null, ThiefGuildLink? HasSequel = null);

public record ThiefGuildLink(string Title, string Url);
public record ThiefGuildSeriesInfo(int ThiefGuildSeriesId, string Name, int Position,
    IReadOnlyList<ThiefGuildSeriesPartInfo> Parts);          // Parts added
public record ThiefGuildSeriesPartInfo(int Position, string Title, string? Url);
```

`ThiefGuildPageParser` gets one extractor per field. Each returns null for missing or unrecognised markup and never throws:

- **`ExtractRating`:**
  - Count: from the `a[href*='/fanmissions/rating_list/']` text, `(\d+)\s+ratings?`.
  - Rating: from the first `<p>` containing an `i.material-icons` whose text is "star". Its `TextContent` is parsed with `(\d+(?:\.\d+)?)` using the invariant culture.
  - If either part is missing, both are null.
- **`ExtractCampaignMissionCount`:** the sidebar `li.list-group-item` text, whitespace-normalised. "Single mission" gives 1. `Campaign of (\d+) missions?` gives N.
- **`ExtractDescription`:** the first `script[type='application/ld+json']`, parsed with `System.Text.Json`. Takes `description` as a string, trimmed. It returns null when the string is empty or when the JSON is invalid, catching `JsonException` only.
- **`ExtractSequelLinks`:**
  - The `li` whose text starts with "Sequel of:" gives its first `a` as `SequelOf`.
  - The `li` containing "FM has a sequel:" gives `HasSequel`.
  - Relative hrefs are made absolute against `https://www.thiefguild.com`.
- **Series parts:** `ExtractSeries` records each member in the walk it already does:
  - For a link, the title comes from its `title` attribute with the regex `\s*\(\d{4}\)\s*$` removed. If there's no `title` attribute, the link text is used. The URL is made absolute.
  - For the current (unlinked) entry, the title is the page's detail-page title (`ExtractDetailPageTitle`) and the URL is null.

The search-card fallback path (a card result with no detail page) has none of these fields, so they're null. That's expected.

## Applying metadata

A new pure helper, `ThiefGuildMetadataApplier.Apply(FanMission mission, ThiefGuildLookupResult result)`, mutates the mission in memory:

- **Always overwritten,** because these fields mirror Thief Guild and are read-only in the app: `ThiefGuildRating`, `ThiefGuildRatingCount`, `CampaignMissionCount`, `Description`, `SequelOf*`, `HasSequel*`. A null in the result clears the field, so the data matches the page.
- **Filled only when blank:** `Author`, `ReleaseYear`, `Tags`. These rules are unchanged.
- **Also:** `ThiefGuildUrl = result.Url`, and `ThiefGuildMetadataVersion = CurrentVersion`.

Series assignment stays in `SeriesAssigner`, updated to the `!SeriesLookupChecked` rule above. After assignment, if the mission is in a series whose `ThiefGuildSeriesId` equals `result.Series.ThiefGuildSeriesId`, the caller runs `ReplacePartsAsync` with the parsed parts. When the result has no series, stored parts are left alone.

**Persistence:**
- **Backfill and refresh:** use a targeted write, `IMissionRepository.ApplyThiefGuildMetadataAsync(missionId, …)`. It replaces `ApplySeriesLookupAsync` and writes only the Thief Guild columns, `ThiefGuildMetadataVersion`, and the series columns (series only when the stored `SeriesId` is null). This keeps the protection against stale copies.
- **Install-time lookup** (`MainViewModel.ApplyThiefGuildMetadataAsync`): applies the metadata to the live in-memory mission and saves it as it does today.
- **Properties** (`MissionEditViewModel`):
  - `LoadFrom` must carry every new column.
  - `SaveAsync` must write them back. This avoids the 3.9.2 bug class, and a round-trip test pins it.
  - Fetch Metadata updates the view model's copies, so Save persists them together with `ThiefGuildMetadataVersion = CurrentVersion`.

## Backfill and Refresh all

`SeriesBackfillService` is renamed `ThiefGuildBackfillService`:

- **`RunAsync(IProgress<(int Done, int Total)>?, CancellationToken, bool refreshAll = false)`**
  - Candidates: every mission with a valid Thief Guild URL, and, unless `refreshAll`, only those with `ThiefGuildMetadataVersion < CurrentVersion`.
  - Order, delay and error handling are the same as today: sequential, a 1-second delay, a per-mission catch, and failures leave the version unchanged so the mission is retried next launch.
  - Per mission: apply the metadata, assign the series, replace the parts, then do the targeted write.
  - Raises `MissionUpdated` after each successful write. This replaces `SeriesAssigned`, because every success now changes visible data.
- **Reloading:** the main window reloads the list at most once every 5 seconds while a run is in progress, plus once at the end. This also resolves the flicker finding parked in 3.9.1.
- **Single run at a time:** `MainViewModel.IsThiefGuildRefreshRunning` is true while the startup backfill or a refresh-all runs. The menu item is disabled while it's true.
- **Refresh Thief Guild Data** (top-level menu):
  - It asks for confirmation first: "Re-fetch Thief Guild data for N linked missions? This takes about N seconds."
  - It then runs `RunAsync(…, refreshAll: true)` with the same status-bar progress: "Refreshing Thief Guild data… n/N".

## Main list UI

- **TG Rating column**
  - Width about 110. Mission rows show "★ 9.02 (229)", and the cell is blank when there's no rating. Header rows are blank.
  - New `SortField.ThiefGuildRating`; missions without a rating sort last in both directions.
- **Type column**
  - Width about 110. Blank when the count is null or 1, "Campaign · 10" otherwise. Header rows are blank.
  - New `SortField.MissionType` (sort key `CampaignMissionCount ?? 1`).
- **Both columns:**
  - Are added to `SortFieldOptions`, the clickable column headers, and `MissionQuery`'s key selector.
  - Sit after Rating. The Tags column still fills the remaining width.
- **`MissingPartRow`** (new `MissionListRow` subtype):
  - Properties: `SeriesPart Part`, plus `DisplayTitle` ("#1 · Dead Letter Box — not in library").
  - `MissionListBuilder.Build` gains the `IReadOnlyList<SeriesPart> parts` input and an `includeMissingParts` flag. `MainViewModel` sets the flag when no filter except Game is active.
  - For an expanded series with parts, the rows are the owned members plus a `MissingPartRow` for each part position that no owned member has. Rows are ordered by position, with owned members that have no position last, as today.
- **Header text:**
  - With parts known and no hidden members: "Name (owned of parts owned)", e.g. "(2 of 3 owned)".
  - When filters hide members: "(shown of owned shown)", unchanged.
  - Without parts: "(N)", unchanged.
- **Rendering:** Title cell dimmed (`MutedForegroundBrush`) and italic. The other cells are empty.
- **Selection and commands:**
  - Selecting a placeholder sets `SelectedMission` to null, so mission commands are disabled, the same as a header.
  - Double-clicking a placeholder opens `Part.ThiefGuildUrl` in the browser (no-op when it's null).
  - The re-selection logic doesn't need to restore placeholders.
- **Context menus:**
  - Placeholder: "Open on Thief Guild".
  - Mission row: "Open on Thief Guild" (only visible when the mission has a URL).
  - Series header: "Open series on Thief Guild" (only when `ThiefGuildSeriesId` is set; URL `https://www.thiefguild.com/fanmissions?series={id}`).
  - Visibility comes from view-model booleans, the same pattern as `IsSeriesHeaderSelected`: `IsMissingPartSelected`, `CanOpenSelectedOnThiefGuild` and `CanOpenSelectedSeriesOnThiefGuild`.

## Properties window

A read-only **Thief Guild** section sits above the Thief Guild URL box and only appears when there's something to show:

- **Summary line:** "★ 9.02 from 229 ratings · Single mission" (or "· Campaign of 10 missions"). Each part is left out when unknown.
- **Description:** a read-only, wrapping, scrollable `TextBox` with a maximum height of about 5 lines.
- **Sequel links:** "Sequel of: <link>" and "Has a sequel: <link>" as `Hyperlink`s that open the browser. These only appear when present and when the mission is not in a series.

The view model exposes `ThiefGuildSummary` (string?), `Description`, `SequelOf`/`HasSequel` (`ThiefGuildLink?`) and `ShowSequelLinks`. Fetch Metadata fills all of these.

## Error handling

- Every extractor is independent and safe. One failing never stops the others.
- The per-mission catch in the backfill is unchanged. A failure leaves the version unchanged, so the mission is retried next launch.
- A refresh that returns no series info never deletes the stored parts or the series link.
- Opening a URL uses `Process.Start` with `UseShellExecute = true`, wrapped in try/catch. A failure is ignored silently, the same as the existing Thief Guild menu item.

## Testing

- **Parser:** fixtures trimmed from the real Endless Rain, The Black Parade and Book of Prophecy Part 3 pages. They cover:
  - rating and count (including missing and malformed)
  - Single mission vs "Campaign of 10 missions"
  - the ld+json description (including invalid JSON giving null)
  - Sequel of / has a sequel with absolute URLs
  - series parts: titles with the year removed, the current mission's own entry, and URLs
- **`ThiefGuildMetadataApplier`:** read-only fields overwritten and cleared; author, year and tags only fill blanks; version stamped.
- **`SeriesAssigner`:** doesn't assign when `SeriesLookupChecked` is set, which covers Ungroup then refresh.
- **Repository:**
  - `ReplacePartsAsync` and `GetAllPartsAsync`
  - parts removed by `DeleteAsync` and `DeleteOrphansAsync`
  - `ApplyThiefGuildMetadataAsync` writes only its columns (stale-copy test)
  - schema upgrade from the 3.9.2 schema
- **`MissionQuery`:** TG rating sort with missing values last in both directions; Type sort.
- **`MissionListBuilder`:**
  - placeholders interleaved by position
  - "(2 of 3 owned)"
  - no placeholders when `includeMissingParts` is false or the series is collapsed
  - series without parts unchanged
- **Backfill:** version filter; `refreshAll` ignores the version; version stamped on success and unchanged on failure; `MissionUpdated` raised; parts replaced.
- **View models:**
  - selecting a placeholder disables commands
  - context-menu booleans
  - the Properties round-trip keeps every new column (regression guard for the 3.9.2 bug class)
  - Fetch fills the Thief Guild section
  - sequel links hidden for series members

## Release

- Bump to **3.9.3** in `AppVersion.cs`, with a `ChangelogData` entry covering the TG Rating and Type columns, the description and sequel links in Properties, placeholders for missing series parts, and Refresh Thief Guild Data.
- The manual run is left to the user: back up the database first, because the first launch re-fetches every linked mission once.
