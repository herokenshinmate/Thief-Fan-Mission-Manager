# Game Groups and Campaign Highlighting — Design

**Date:** 2026-09-25
**Status:** Draft for review
**Builds on:** `2026-09-25-fm-series-design.md` (3.9.1) and `2026-09-25-tg-metadata-design.md` (3.9.3)

## Goal

1. The main mission list is divided into **one big, collapsible banner per game**, Thief 1 then Thief 2. Everything else (sorting, series headers, placeholders) nests inside its game.
2. **Campaigns stand out.** This covers packed campaigns (one FM containing several missions, `CampaignMissionCount > 1`) and series (separate FMs grouped under a series header). Both get a coloured accent bar and a badge.

### Success criteria

- Every visible mission sits under its game's banner.
- The banner shows the game icon and name in large type, and a stats line with the mission, completed and installed counts.
- Each banner collapses and expands. Its state is remembered between launches.
- Packed-campaign rows show a gold accent bar and a gold "CAMPAIGN · N" badge.
- Series headers, series members and missing-part placeholders show a blue accent bar, and series headers also show a blue "SERIES" badge.
- Selecting a banner disables mission commands, the same as selecting a series header or a placeholder.

### Out of scope

- Grouping by anything other than game (author, status, and so on).
- Collapse-all or expand-all actions.
- Changing how series or placeholders work within a game.

## List structure

### New row type

```csharp
public sealed class GameHeaderRow : MissionListRow
{
    GameTitle Game; bool IsExpanded;
    int ShownCount;      // missions of this game passing the filters
    int TotalCount;      // all missions of this game in the library
    int CompletedCount;  // shown missions with Status == Completed
    int InstalledCount;  // shown missions with InstallStatus == Installed
    string ChevronGlyph; // "▼" expanded, "▶" collapsed
    string StatsText;
}
```

- `StatsText` is "42 missions · 12 completed · 30 installed" when nothing is hidden.
- When filters hide some missions it reads "12 of 42 missions shown · 3 completed · 10 installed".
- Singular and plural forms are correct ("1 mission").

### `MissionListBuilder.Build`

Gains a `IReadOnlySet<GameTitle> collapsedGames` parameter (optional, default empty).

- The filtered, sorted missions are split by `Game`. Games appear in enum order: `Thief1`, then `Thief2`.
- Each game with at least one filtered mission emits a `GameHeaderRow`. A game with no visible missions gets no banner.
- If the game is expanded, today's logic (standalone units, series grouping, placeholders, Title re-sort, member ordering) runs on **that game's missions only**, and its rows follow the banner. A collapsed game emits only its banner.
- **Series spanning both games:** each game shows the series header with that game's members.
  - The header counts (`ShownCount`/`TotalCount`/`CompletedCount`, `CommonGame`) are computed from that game's members.
  - For "(x of y owned)", `TotalCount` is owned members *of that game*, and the part count is unchanged.
  - Placeholders for missing parts are emitted only under the game that holds the series' lowest-positioned *shown* member (ties go to Thief1). If the Game filter hides that game, the placeholders then appear under the visible one. A part is "missing" when **no** owned member in **any** game has its position, which is the same rule as today, using all missions.
- **The Game filter** still works as before. With a game selected, only that game's banner shows.

### Campaign flags

- **`MissionRow.IsPackedCampaign`:** `Mission.CampaignMissionCount > 1`.
- **`MissionRow.IsSeriesRelated`:** `IsSeriesMember`, meaning the mission is shown under a series header.
- **`SeriesHeaderRow.IsSeriesRelated`:** always true.
- **`MissingPartRow.IsSeriesRelated`:** always true.
- **Accent priority:** `MissionListRow` gets a virtual `AccentKind` (`None`, `Campaign`, `Series`), overridden per row type. A packed campaign that is also a series member shows **Campaign**, because the rarer highlight wins. `GameHeaderRow` is `None`.
- **Badge:** `MissionRow.CampaignBadgeText` is "CAMPAIGN · N" when `IsPackedCampaign`, otherwise null.

## Persistence

`AppSettings` gains `bool Thief1Collapsed` and `bool Thief2Collapsed`, both added as `INTEGER NOT NULL DEFAULT 0` through `SchemaUpgrader.AddColumnIfMissing`.

`ISettingsRepository` gains `Task SetGameCollapsedAsync(GameTitle game, bool collapsed)`:
- It's a targeted write of just that column.
- It creates the settings row (Id 1) if it's missing.
- The existing `SaveAsync` (from the Settings dialog) already copies only the folder and exe fields onto the stored row, so it can't reset the collapsed flags.
- A test pins that down.
- `FakeSettingsRepository` mirrors both methods, and its `GetAsync` copies the two new fields.

## View model

`MainViewModel`:
- Takes `ISettingsRepository` as a new, last constructor parameter.
- Loads the collapsed set in `LoadAsync` from `GetAsync()`.
- Passes it to `Build`.

It adds:
- `IAsyncRelayCommand<GameHeaderRow?> ToggleGameExpandedCommand`. A null parameter means the selected banner. It flips the game in the set, persists it with `SetGameCollapsedAsync`, and calls `ApplyQuery()`.
- `bool IsGameHeaderSelected`.

Changes to existing behaviour:
- `ShowMissionMenuItems` becomes false for `GameHeaderRow`.
- Selecting a banner sets `SelectedMission` to null, the same as a series header.
- The notifications in `OnSelectedRowChanged` now include `IsGameHeaderSelected`.
- **Re-selection in `ApplyQuery`:** the previously selected game banner is restored by game. When the game holding the selected mission is collapsed, selection falls back to that game's banner, just as a collapsed series falls back to its series header.

## UI

- **Row containers:** the ListView switches from a fixed `ItemContainerStyle` to an `ItemContainerStyleSelector` (`MissionListItemStyleSelector`). It returns `GameBannerItemStyle` for `GameHeaderRow` and the existing `GridViewListViewItemStyle` for everything else.
- **`GameBannerItemStyle`:** a `ControlTemplate` that does **not** use `GridViewRowPresenter`. Instead it has a full-width `Border` containing:
  - a slightly tinted background (a new `BannerBackgroundBrush`), a 1 px `ControlBorderBrush` border, corner radius 4, a top margin of 8 (none for the first banner) and padding 8,6;
  - a chevron button bound to `ToggleGameExpandedCommand` with the row as its parameter;
  - the game icon at 32 × 32 (the same `GameTitleToIconSourceConverter` / `GameTitleToBrushConverter` pair used by the Game column);
  - the game display name at FontSize 18, SemiBold;
  - `StatsText` below it at FontSize 12 in `MutedForegroundBrush`.

  Hover and selection get a subtle border highlight rather than the row's fill.
- **Accent bar:** `GridViewListViewItemStyle`'s template gains a 4 px-wide `Border` docked left, outside the part that changes colour on hover and selection. Its background comes from a `RowAccentToBrushConverter`: `Campaign` gives `CampaignAccentBrush` (#FFC9A227), `Series` gives `SeriesAccentBrush` (#FF64B5F6), and `None` is transparent. The bar is always 4 px, so rows stay aligned.
- **Badges** (in the Title cell templates):
  - The mission template shows a rounded badge before the title when `CampaignBadgeText` isn't null: gold text on a translucent gold background, FontSize 10, SemiBold.
  - The series header template shows a "SERIES" badge styled the same way in blue, between the book icon and the header text.
- **Interaction:**
  - Double-clicking a banner toggles it.
  - The context menu gains "Expand / Collapse Game", visible only when `IsGameHeaderSelected`. The mission items are already hidden through `ShowMissionMenuItems`, and the series items through `IsSeriesHeaderSelected`.

## Testing

- **`MissionListBuilderTests`:**
  - banners in game order, each followed by its rows;
  - a collapsed game emits only its banner;
  - a game with no filtered missions gets no banner;
  - the stats text with and without filters, including singular and plural;
  - sort order stays within each game;
  - a series spanning both games shows under each game, with placeholders only under the game of the first shown part;
  - `AccentKind` and `CampaignBadgeText` on each row type, including campaign-wins-over-series;
  - existing tests are updated to expect a leading banner. A small helper (for example, dropping `GameHeaderRow`s) keeps their intent readable.
- **`MainViewModelTests`:**
  - selecting a banner disables mission commands and hides the mission menu items;
  - toggling persists through the settings repository and collapses the rows;
  - the collapsed state is loaded on `Load`;
  - collapsing the game of the selected mission selects its banner;
  - existing `VisibleRows[index]` assertions are updated for the leading banner.
- **Settings:**
  - `SetGameCollapsedAsync` round-trip;
  - `SaveAsync` from the Settings dialog keeps the collapsed flags;
  - the schema upgrade adds the two columns.
- **UI:** build, the full test suite, and careful reading of the XAML. The app is not launched; the user does the manual check.

## Release

Version **3.9.4**, with a changelog entry for the game banners and the campaign/series highlighting.
