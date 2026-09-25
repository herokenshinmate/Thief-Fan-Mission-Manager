# Thief FM Manager

[![Latest release](https://img.shields.io/github/v/release/herokenshinmate/Thief-Fan-Mission-Manager)](https://github.com/herokenshinmate/Thief-Fan-Mission-Manager/releases/latest)
[![Release workflow](https://img.shields.io/github/actions/workflow/status/herokenshinmate/Thief-Fan-Mission-Manager/release.yml?label=release)](https://github.com/herokenshinmate/Thief-Fan-Mission-Manager/actions/workflows/release.yml)
[![Downloads](https://img.shields.io/github/downloads/herokenshinmate/Thief-Fan-Mission-Manager/total)](https://github.com/herokenshinmate/Thief-Fan-Mission-Manager/releases)
[![License: GPL v3](https://img.shields.io/badge/license-GPL--3.0-blue)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)](#installing)

A Windows desktop app for cataloging and tracking fan missions (FMs) for
**Thief: The Dark Project** and **Thief II: The Metal Age**. It keeps track
of what you've installed, what you've played, your ratings and notes, and
pulls in ratings and details from [Thief Guild](https://www.thiefguild.com)
so you don't have to track any of it by hand.

**📖 New here? See [`docs/USAGE.md`](docs/USAGE.md) for a full walkthrough
of setup, cataloging, and everyday use.**

<img width="1297" height="648" alt="image" src="https://github.com/user-attachments/assets/59c0a1e2-5700-4c0e-89b6-8ec8b581a294" />

## Features

- **Catalog** every FM for both games in one list — status (Not Played,
  In Progress, Completed, Abandoned), your rating, tags, author, and notes.
- **Series and campaigns** are grouped automatically: multi-part series show
  a collapsible header with how many parts you own and have completed, and
  the parts you don't own yet appear as dimmed placeholders with a link to
  their Thief Guild page.
- **Thief Guild integration**: installing a mission looks up its author,
  release year, tags, community rating, and description automatically.
  Refresh ratings for everything at once from the menu.
- **Scan for new missions** in your configured FM and Downloads folders —
  the app recognizes an archive as already installed even if its filename
  differs slightly (casing, version tags, bracketed notes), and lets you
  ignore folders you never want to see again.
- **Install straight from Downloads**: pick an archive (zip/7z/rar) and the
  app extracts it into the right game's FM folder for you.
- **Launch** a mission directly from the list — the Play button jumps
  straight into the mission instead of opening the loader's own picker.
- **Self-updating**: new versions download in the background and a
  "Restart to update" link appears when one's ready. See Help > Changelog
  for what changed, or Check for Updates in About.

## Installing

1. Download the latest `ThiefFMManager-win-Setup.exe` from
   [Releases](https://github.com/herokenshinmate/Thief-Fan-Mission-Manager/releases/latest).
2. Run it. Windows SmartScreen may warn that the app is unsigned (builds
   aren't code-signed) — click **More info > Run anyway**.
3. On first launch, you'll be prompted to set the FM folder and game
   executable for each Thief game you have installed.

Prefer not to install anything? Download
`ThiefFMManager-win-Portable.zip` instead and run `ThiefManager.exe`
straight from the extracted folder. The portable build does **not**
auto-update — check Releases yourself for new versions.

### Updating

Installed copies check for updates automatically and download them in the
background. When one's ready, a **Restart to update** link appears in the
status bar — click it, or update later from the About window's
**Check for Updates** button.

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
git clone https://github.com/herokenshinmate/Thief-Fan-Mission-Manager.git
cd Thief-Fan-Mission-Manager
dotnet build
dotnet run --project src/ThiefManager
```

Run the tests with:

```
dotnet test tests/ThiefManager.Tests
```

Releases are built and published automatically by GitHub Actions when a
version tag is pushed — see [`docs/RELEASING.md`](docs/RELEASING.md) for
the full process.

## Tech stack

- **.NET 8 / WPF**, Windows-only, using [WPF-UI](https://wpfui.lepo.co/)
  for Fluent Design styling.
- **MVVM** via `CommunityToolkit.Mvvm`.
- **SQLite** (via EF Core) for local storage — a single `.db` file in your
  user app-data folder, no server or account required.
- **[Velopack](https://velopack.io)** for the installer and self-updates.

## A note on how this was built

This app was built with the help of Claude (Anthropic's AI). It's a
personal project made for my own use and shared publicly in case it's
useful to other Thief fans — I'm the sole developer and maintainer.

## License

[GPL-3.0](LICENSE) — you're free to use, modify, and redistribute this
code, including commercially, as long as derivative works are also
distributed under GPL-3.0 with source available.
