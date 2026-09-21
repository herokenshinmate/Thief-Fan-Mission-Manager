# Thief Fan Mission Manager — Design

## Purpose

A personal desktop app to catalog and track Thief: The Dark Project and
Thief 2 fan missions in one place: what's installed, whether it's been
played, and a personal rating/notes. Today this information (if tracked
at all) lives only in loose memory or scattered notes; the loaders
(NewDarkLoader/FMSel) list installed FMs but don't track play status,
ratings, or personal notes.

## Scope (v1)

- Catalog fan missions for both Thief 1 and Thief 2.
- Track per-mission: title, game, author, release year, status, rating,
  tags, notes, date started/completed, source folder path.
- Scan a configured FM folder to discover installed missions not yet in
  the catalog, and let the user confirm/edit them before adding.
- Launch the configured game/loader executable for a selected game from
  within the app.
- Filter/sort the catalog (by game, status, rating, tags).

Out of scope for v1:
- Automatically selecting/injecting a specific FM into FMSel's launch
  (no reliable silent-select CLI across FMSel versions) — launching
  opens the configured exe/loader, which then shows its own FM list.
- Online sync, mission downloading, or fetching metadata from the
  internet (e.g. TTLG mission database).
- Multi-user / cloud storage.

## Tech Stack

- **.NET 8, WPF** desktop app, Windows-only.
- **MVVM** via `CommunityToolkit.Mvvm` (lightweight source-generator
  based MVVM — no need for a heavier framework like Prism).
- **SQLite** for storage via EF Core's Sqlite provider — single local
  `.db` file in the user's app-data folder, no server component.

Rejected alternative: Avalonia (cross-platform UI) — unnecessary since
the user is Windows-only; WPF has less setup friction and mature
tooling for a single-developer desktop app.

## Data Model

`FanMission` (one table):

| Field | Type | Notes |
|---|---|---|
| Id | int (PK) | |
| Title | string | required; defaults to folder name on scan |
| Game | enum: Thief1, Thief2 | |
| Author | string? | optional |
| ReleaseYear | int? | optional |
| Status | enum: NotPlayed, InProgress, Completed, Abandoned | default NotPlayed |
| Rating | int? | 0–5 scale |
| Tags | string | comma-separated (simple v1; can normalize to a table later if needed) |
| Notes | string? | freeform personal review/notes |
| DateStarted | DateTime? | |
| DateCompleted | DateTime? | |
| FolderPath | string | absolute path to the FM's folder, used for de-duping on scan |

`AppSettings` (single row or simple settings file/table):

- Thief1FmFolder, Thief2FmFolder — paths to scan for installed FMs.
- Thief1ExePath, Thief2ExePath — executables the "Play" action launches
  (points at FMSel or the game exe directly, per the user's setup).

## Key Flows

**Scanning for new missions**
1. User opens Settings, sets the FM folder path(s) for Thief 1 / Thief 2
   (each loader's `fms/` directory) and the exe paths.
2. On the main screen, "Scan for New Missions" (per game) lists
   immediate subfolders of the configured FM folder whose `FolderPath`
   isn't already in the catalog.
3. Each candidate shows a pre-filled `Title` (folder name, editable) and
   `Game`; user can select which to import, edit title inline, then
   confirm to add them as `NotPlayed` rows.

**Cataloging / editing**
- Main window: list of missions (sortable/filterable by Game, Status,
  Rating, Tags) on the left; selecting one shows an edit panel with all
  fields on the right. Manual "Add Mission" also available without
  scanning.

**Launching**
- "Play" button on a mission (or on a game filter) launches
  `Process.Start` on the configured exe path for that mission's Game.
  No FM auto-selection — the loader's own UI takes it from there. If no
  exe path is configured, the button prompts the user to set one in
  Settings.

## Error Handling

- Scan: skip and report (not crash) folders it can't read (permissions,
  missing path) — show a summary of skipped items.
- Launch: if exe path is missing/invalid, show an inline message
  pointing to Settings rather than a raw exception.
- Storage: EF Core migrations create the SQLite file/schema on first
  run if it doesn't exist.

## Testing

- Unit tests for the scan logic (given a folder listing, which
  candidates are new vs. already-cataloged) and for filter/sort logic —
  these are pure functions independent of WPF/EF, so testable without a
  UI or real database.
- Manual end-to-end check: run the app, add a mission manually, scan a
  real (or test) FM folder, edit fields, launch with a dummy exe path
  (e.g. notepad.exe) to confirm the launch mechanism works.
