# Installer and Updates — Design

**Date:** 2026-09-25
**Status:** Draft for review

## Goal

Thief FM Manager is distributed publicly to the Thief community through GitHub Releases:

- It has a friendly, per-user installer with no .NET prerequisite.
- The app finds updates itself, downloads them in the background, and applies them only when the user clicks **Restart to update**.
- Releases are built and published automatically from a git tag.

### Success criteria

- Pushing a tag `vX.Y.Z` builds, tests, packages and publishes a GitHub Release. The release contains `ThiefFMManager-win-Setup.exe`, full and delta update packages, and release notes taken from that version's changelog entry.
- Setup installs per user under `%LocalAppData%`, with no admin rights or UAC prompt. It adds a Start-menu shortcut named "Thief FM Manager" and an uninstall entry. Users don't need .NET installed.
- On launch, an installed copy checks GitHub, downloads any newer release in the background, and shows "Version X is ready — Restart to update · What's new" in the status bar. Nothing changes until the user clicks.
- The About window has a **Check for Updates** button.
- User data in `%LocalAppData%\ThiefManager` (the SQLite catalog) is never touched by install, update or uninstall.
- An optional code-signing step exists in the pipeline. It's off until a signing secret is configured.

### Out of scope

- Code signing itself. Builds are unsigned for now, and the README explains the SmartScreen prompt.
- Pre-release or beta channels.
- Machine-wide (Program Files) installs, MSIX, Microsoft Store.
- Non-Windows builds.
- The README. That's the next task, and it will document installing and updating.

## Technology

- **Velopack**: NuGet package `Velopack` 1.2.158 in the app, and the CLI `vpk` 1.2.158 in CI, pinned to the same version.
- **GitHub Actions** on `windows-latest` with .NET 8.
- **The update source** is the GitHub repo `https://github.com/herokenshinmate/ThiefFMManager`, via Velopack's `GithubSource`. Releases are public, so the app doesn't need a token.

## Versioning

- `AppVersion.Current` in `src/ThiefManager/AppVersion.cs` stays the single source of truth, with patch bumps as before.
- The tag for a release is `v` + `AppVersion.Current`.
- The workflow fails before building if:
  - the tag doesn't match `AppVersion.Current`, or
  - `ChangelogData.Entries` has no entry for that version.
- The csproj `<Version>` is set from the tag at publish time (`-p:Version=`), so the assembly and file versions match too.

## Release pipeline (`.github/workflows/release.yml`)

Trigger: `push` of tags matching `v*.*.*`. It also has `workflow_dispatch` with a `tag` input, so a failed release can be re-run. The job needs `permissions: contents: write`.

Steps:
1. Check out the repository and set up .NET 8.
2. **Verify the version.** `build/Get-ReleaseNotes.ps1 -Version X.Y.Z` does two things:
   - It checks `AppVersion.cs` against the tag version.
   - It extracts that version's changelog bullets from `ChangelogEntry.cs` into `release-notes.md`, formatted as a markdown list.

   The script exits non-zero on a mismatch or a missing entry.
3. `dotnet test tests/ThiefManager.Tests -c Release`.
4. `dotnet publish src/ThiefManager -c Release -r win-x64 --self-contained -p:Version=X.Y.Z -o publish`.
5. Install `vpk` 1.2.158 as a dotnet global tool.
6. `vpk download github --repoUrl <repo> --token <GITHUB_TOKEN>` into `releases/`. This fetches the previous release so a delta can be built. On the very first release there's nothing to download, and that isn't an error.
7. **Optional signing.** This only happens when the repo secret `SIGN_PARAMS` is set. If it is, `--signParams "<secret>"` is added to the pack command. If it isn't, the step is skipped and the log says "unsigned build".
8. `vpk pack` with these arguments:
   - `--packId ThiefFMManager`
   - `--packVersion X.Y.Z`
   - `--packDir publish`
   - `--mainExe ThiefManager.exe`
   - `--packTitle "Thief FM Manager"`
   - `--icon src/ThiefManager/Assets/AppIcon.ico`
   - `--releaseNotes release-notes.md`
   - `--outputDir releases`
9. `vpk upload github --repoUrl <repo> --token <GITHUB_TOKEN> --publish --releaseName "Thief FM Manager X.Y.Z" --tag vX.Y.Z --outputDir releases`.

`docs/RELEASING.md` documents how to cut a release: bump the version, add the changelog entry, commit, push the tag, then watch the workflow. It also explains how to turn on signing later.

## App integration

### Startup hook

Velopack's hook must run before any WPF startup.

- Add `src/ThiefManager/Program.cs` with `[STAThread] static void Main(string[] args)`. It calls `VelopackApp.Build().Run();`, then `var app = new App(); app.InitializeComponent(); app.Run();`.
- In the csproj, `App.xaml` changes from `ApplicationDefinition` to `Page`, and `<StartupObject>ThiefManager.Program</StartupObject>` is set.
- `App.OnStartup` doesn't change.

### Update service

```csharp
public record AvailableUpdate(string Version, string? ReleaseNotesMarkdown);

public interface IUpdateService
{
    /// <summary>False when running outside a Velopack install (e.g. a dev build); all update features are then inert.</summary>
    bool IsInstalled { get; }

    /// <summary>Checks GitHub for a newer release and downloads it. Returns it once ready to apply, or null if up to date.</summary>
    Task<AvailableUpdate?> CheckAndDownloadAsync();

    /// <summary>Applies the downloaded update and restarts the app.</summary>
    void ApplyAndRestart();
}
```

`VelopackUpdateService` implements this with `UpdateManager(new GithubSource("https://github.com/herokenshinmate/ThiefFMManager", null, false))`:

- It keeps the `UpdateInfo` returned by `CheckForUpdatesAsync` so it can pass it to `DownloadUpdatesAsync` and later to `ApplyUpdatesAndRestart`.
- `IsInstalled` maps to `UpdateManager.IsInstalled`.
- It isn't unit-tested, because it's a direct wrapper around Velopack and the network.

### View model

`MainViewModel` takes `IUpdateService` as a new last constructor parameter and adds:

- **Observable properties:**
  - `string? UpdateReadyVersion`
  - `string? UpdateReleaseNotes`
  - `string? UpdateCheckMessage`: the result of a manual check, shown in the About window
  - `bool IsUpdateReady`, true when `UpdateReadyVersion` isn't null
- **`Task CheckForUpdatesOnStartupAsync()`:**
  - If the app isn't installed, it does nothing.
  - Otherwise it calls `CheckAndDownloadAsync` and swallows any exception. A startup check never shows an error.
  - When an update comes back, it sets `UpdateReadyVersion` and `UpdateReleaseNotes`.
- **`IAsyncRelayCommand CheckForUpdatesCommand`.** A manual check sets `UpdateCheckMessage` to one of:
  - not installed: "Updates are only available in the installed version."
  - up to date: "You're up to date (version {AppVersion.Current})."
  - update ready: "Version {v} is ready — restart to update." It also sets the ready state.
  - on an exception: "Couldn't check for updates: {message}".

  While the check runs, the message is "Checking for updates…".
- **`bool TryRestartToUpdate(bool confirmedDespiteRefresh)`:**
  - It does nothing unless an update is ready.
  - If `IsThiefGuildRefreshRunning` is true and the caller hasn't confirmed, it returns false so the window can ask the user.
  - Otherwise it calls `ApplyAndRestart()` and returns true.

### UI

- **Status bar.** The left side of the bottom bar already shows the background status text. When `IsUpdateReady` is true, an update notice replaces it:
  - it reads "⬆ Version {UpdateReadyVersion} is ready — ";
  - a **Restart to update** hyperlink-style button follows;
  - then " · ";
  - then a **What's new** link.
- **Restart to update:**
  - The window calls `TryRestartToUpdate(false)`.
  - If that returns false, the window shows the dialog "A Thief Guild refresh is running. Restart to update anyway? It will resume next time." Confirming calls `TryRestartToUpdate(true)`.
- **What's new** shows the release notes in a simple read-only dialog, a WPF-UI `MessageBox` with a scrollable TextBlock. It falls back to "No release notes." when there aren't any.
- **Startup.** `MainWindow` calls `CheckForUpdatesOnStartupAsync()` after the initial `LoadCommand`. It runs concurrently with the Thief Guild backfill and isn't awaited before it.
- **About window.**
  - It shows the current version.
  - It gets a **Check for Updates** button bound to `CheckForUpdatesCommand`, with `UpdateCheckMessage` shown beneath it.
  - When an update is ready, it also shows a **Restart to update** button. This follows the same rule as the status bar: the window calls `TryRestartToUpdate(false)`, and if that returns false it asks the user to confirm first.
  - The About window uses the `MainViewModel` as its `DataContext`.

### Wiring

`App.xaml.cs` creates `new VelopackUpdateService()` and passes it to `MainViewModel`. Tests use a `FakeUpdateService`.

## Error handling

- **Startup check:** every exception is swallowed. Offline users see nothing.
- **Manual check:** the exception message is shown in `UpdateCheckMessage`.
- **`ApplyAndRestart`:** a failure throws inside Velopack. The window catches it and shows an error dialog ("Couldn't apply the update: …"), and the app keeps running.
- **Release workflow:** each step fails the job on error. `vpk download` with no prior release isn't treated as an error.

## Testing

- **`MainViewModelTests`, using `FakeUpdateService`:**
  - The startup check sets the ready state when an update is returned.
  - It does nothing when not installed.
  - It swallows exceptions.
  - The manual check produces each of the four messages.
  - `TryRestartToUpdate` refuses while a refresh is running and unconfirmed, applies when confirmed, and does nothing without an update.
- **`ReleaseNotesScriptTests`:** runs `build/Get-ReleaseNotes.ps1` through `powershell -NoProfile -File` against the repo's real files, for the current `AppVersion.Current`. It asserts:
  - exit code 0;
  - `release-notes.md` starts with "- " bullets matching that changelog entry;
  - a version that doesn't match makes the script exit non-zero.
- **End to end:**
  - Tag `v3.9.7`, which bundles this feature. The user downloads the Setup.exe and installs it.
  - Then a tiny `v3.9.8` is released, and the status bar should offer the update and apply it.
  - This is the pipeline's real acceptance test.

## Release

- 3.9.7 contains this feature, with a changelog entry for automatic updates and the new installer.
- 3.9.8 is a follow-up changelog-only release used to prove updates work.
