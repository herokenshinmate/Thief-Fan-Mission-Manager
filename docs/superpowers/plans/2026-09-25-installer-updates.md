# Installer and Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Thief FM Manager as a Velopack per-user installer, published to GitHub Releases by a tag-triggered GitHub Actions workflow. The app checks for, downloads and (on the user's click) applies updates.

**Architecture:**
- **Release notes script:** `build/Get-ReleaseNotes.ps1` checks that the tag matches `AppVersion.Current` and turns that version's `ChangelogData` entry into markdown release notes.
- **Release workflow:** tests, publishes self-contained win-x64, packs with `vpk` (including deltas against the previous release, and optional signing), then uploads to GitHub Releases.
- **Startup hook:** the app gets a custom `Program.Main` that runs `VelopackApp.Build().Run()` before WPF starts.
- **Update service:** `VelopackUpdateService` wraps `UpdateManager` + `GithubSource` behind `IUpdateService`, so `MainViewModel`'s update state is unit-testable with a fake.

**Tech Stack:** .NET 8 WPF, WPF-UI 4.3, CommunityToolkit.Mvvm 8.4, Velopack 1.2.158 (NuGet) and vpk 1.2.158 (CLI), GitHub Actions (windows-latest), PowerShell, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-25-installer-updates-design.md`

## Global Constraints

- **Velopack version:** use exactly `1.2.158` for both the NuGet package and the `vpk` tool.
- **Repo and package names:**
  - Repo URL: `https://github.com/herokenshinmate/ThiefFMManager`
  - Package id: `ThiefFMManager`
  - Main exe: `ThiefManager.exe`
  - Pack title: `Thief FM Manager`
  - Runtime: `win-x64`, self-contained
- **Version source:** `AppVersion.Current` is the single source of truth. A release tag is `v` + that version, and the workflow must fail if they differ or if `ChangelogData` has no entry for the version.
- **Startup check stays silent:** any failure is swallowed, and a non-installed (dev) run does nothing.
- **Nothing is applied without a click:** an update is only ever applied when the user clicks.
- **Signing:** only happens when the `SIGN_PARAMS` repo secret is set. Otherwise the build is unsigned and the log says so.
- **User data:** anything in `%LocalAppData%\ThiefManager` is never touched.
- **Versioning:** a patch bump to `3.9.7`, done in Task 4 only.
- **Code style:** file-scoped namespaces, `[ObservableProperty]` fields, and doc comments only where the reason isn't obvious.
- **Commands:**
  - Build: `dotnet build src/ThiefManager`, expecting 0 warnings.
  - Test: `dotnet test tests/ThiefManager.Tests`. The baseline is 252 passing.
- **Commit trailer:** every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- **Don't launch the app, don't push tags, don't create GitHub releases.** The controller does the first real release with the user after merge.

## Review Focus

1. **A changelog line containing escaped quotes** (e.g. `\"Delete from Library...\"` in 3.9.6) must come out of the release-notes script with real quotes, exactly as it appears in the app's Changelog window. Test: `GetReleaseNotes_ForCurrentVersion_MatchesChangelogData` (Task 1) compares against the real 3.9.6+ entries.
2. **A tag that doesn't match `AppVersion.Current`** must stop the release before anything is built. Test: `GetReleaseNotes_WithMismatchedVersion_Fails` (Task 1).
3. **A dev build** (not installed) must never error or show update UI. It shows only an informative message on a manual check. Tests: `CheckForUpdatesOnStartup_WhenNotInstalled_DoesNothing` and `CheckForUpdates_WhenNotInstalled_ExplainsWhy` (Task 2).
4. **Being offline at startup** shows nothing. Test: `CheckForUpdatesOnStartup_SwallowsFailures` (Task 2).
5. **Clicking Restart during a Thief Guild refresh** asks first. Test: `TryRestartToUpdate_DuringRefresh_NeedsConfirmation` (Task 2).

---

### Task 1: Release-notes and version-check script

**Files:**
- Create: `build/Get-ReleaseNotes.ps1`, `tests/ThiefManager.Tests/Build/ReleaseNotesScriptTests.cs`

**Interfaces:**
- Produces: `build/Get-ReleaseNotes.ps1 -Version <X.Y.Z> [-OutFile <path>]`.
  - Exits 0 and writes markdown bullets when the version matches `AppVersion.Current` and has a changelog entry.
  - Otherwise it prints the reason to stderr and exits 1.

- [ ] **Step 1: Write the failing tests**

`tests/ThiefManager.Tests/Build/ReleaseNotesScriptTests.cs`:
```csharp
using System.Diagnostics;
using Xunit;

namespace ThiefManager.Tests.Build;

public class ReleaseNotesScriptTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ThiefManager.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static (int ExitCode, string StdErr) RunScript(string version, string outFile)
    {
        var root = RepoRoot();
        var start = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(root, "build", "Get-ReleaseNotes.ps1")}\" -Version {version} -OutFile \"{outFile}\"")
        {
            WorkingDirectory = root,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(start)!;
        var stdErr = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdErr);
    }

    [Fact]
    public void GetReleaseNotes_ForCurrentVersion_MatchesChangelogData()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"release-notes-{Guid.NewGuid()}.md");
        try
        {
            var (exitCode, stdErr) = RunScript(AppVersion.Current, outFile);

            Assert.True(exitCode == 0, stdErr);
            var expected = ChangelogData.Entries.Single(e => e.Version == AppVersion.Current).Changes.Select(c => "- " + c);
            Assert.Equal(expected, File.ReadAllLines(outFile));
        }
        finally
        {
            File.Delete(outFile);
        }
    }

    [Fact]
    public void GetReleaseNotes_WithMismatchedVersion_Fails()
    {
        var outFile = Path.Combine(Path.GetTempPath(), $"release-notes-{Guid.NewGuid()}.md");

        var (exitCode, stdErr) = RunScript("0.0.1", outFile);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("AppVersion.Current", stdErr);
        Assert.False(File.Exists(outFile));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter ReleaseNotesScriptTests`
Expected: FAIL, because the script file doesn't exist and PowerShell reports that it can't find it.

- [ ] **Step 3: Implement the script**

`build/Get-ReleaseNotes.ps1`:
```powershell
<#
.SYNOPSIS
    Verifies a release version against AppVersion.Current and writes that version's changelog
    entry as markdown release notes.
.DESCRIPTION
    Used by the release workflow before anything is built: a tag that doesn't match the app's
    version, or a version with no changelog entry, stops the release.
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutFile = 'release-notes.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Fail([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 1
}

$appVersionSource = Get-Content -Raw (Join-Path $repoRoot 'src/ThiefManager/AppVersion.cs')
$appVersionMatch = [regex]::Match($appVersionSource, 'Current\s*=\s*"([^"]+)"')
if (-not $appVersionMatch.Success) {
    Fail "Couldn't read AppVersion.Current from src/ThiefManager/AppVersion.cs."
}
$appVersion = $appVersionMatch.Groups[1].Value
if ($appVersion -ne $Version) {
    Fail "Release version $Version doesn't match AppVersion.Current ($appVersion). Bump AppVersion.cs or fix the tag."
}

$changelogSource = Get-Content -Raw (Join-Path $repoRoot 'src/ThiefManager/ChangelogEntry.cs')
$entryPattern = 'new\("' + [regex]::Escape($Version) + '",\s*"[^"]*",\s*new\[\]\s*\{(?<body>.*?)\}\)'
$entry = [regex]::Match($changelogSource, $entryPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $entry.Success) {
    Fail "No ChangelogData entry for version $Version in src/ThiefManager/ChangelogEntry.cs."
}

# Each change is a C# string literal; unescape \" and \\ so the notes read exactly like the app's Changelog window.
$bullets = @([regex]::Matches($entry.Groups['body'].Value, '"((?:[^"\\]|\\.)*)"') | ForEach-Object {
    '- ' + ($_.Groups[1].Value -replace '\\"', '"' -replace '\\\\', '\')
})
if ($bullets.Count -eq 0) {
    Fail "The ChangelogData entry for version $Version has no changes listed."
}

$fullOutPath = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path (Get-Location) $OutFile }
# WriteAllLines writes UTF-8 without a BOM, which vpk reads cleanly as markdown.
[System.IO.File]::WriteAllLines($fullOutPath, [string[]]$bullets)
Write-Host "Release notes for $Version written to $fullOutPath"
```

- [ ] **Step 4: Run to verify pass**

Run the filter from Step 2, then the full suite once.
Expected: all pass (252 + 2 = 254).

- [ ] **Step 5: Commit**

```bash
git add build/Get-ReleaseNotes.ps1 tests/ThiefManager.Tests/Build/ReleaseNotesScriptTests.cs
git commit -m "Add release-notes and version-check script for releases"
```

---

### Task 2: Velopack startup hook, update service and view-model state

**Files:**
- Create:
  - `src/ThiefManager/Program.cs`
  - `src/ThiefManager/Services/IUpdateService.cs`
  - `src/ThiefManager/Services/VelopackUpdateService.cs`
  - `tests/ThiefManager.Tests/Fakes/FakeUpdateService.cs`
- Modify:
  - `src/ThiefManager/ThiefManager.csproj`
  - `src/ThiefManager/ViewModels/MainViewModel.cs`
  - `src/ThiefManager/App.xaml.cs`
  - `tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs`

**Interfaces:**
- Produces:
  - `record AvailableUpdate(string Version, string? ReleaseNotesMarkdown)`
  - `IUpdateService` with `IsInstalled`, `CheckAndDownloadAsync()` and `ApplyAndRestart()`
  - `VelopackUpdateService`
  - New `MainViewModel` constructor parameter `IUpdateService updateService`, added last
  - New `MainViewModel` members:
    - observable `UpdateReadyVersion`, `UpdateReleaseNotes` and `UpdateCheckMessage`
    - `bool IsUpdateReady`
    - `IAsyncRelayCommand CheckForUpdatesCommand`
    - `Task CheckForUpdatesOnStartupAsync()`
    - `bool TryRestartToUpdate(bool confirmedDespiteRefresh)`, which returns false only when the caller must ask the user first

- [ ] **Step 1: Add the service types and the fake**

`src/ThiefManager/Services/IUpdateService.cs`:
```csharp
namespace ThiefManager.Services;

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
`tests/ThiefManager.Tests/Fakes/FakeUpdateService.cs`:
```csharp
using ThiefManager.Services;

namespace ThiefManager.Tests.Fakes;

public class FakeUpdateService : IUpdateService
{
    public bool IsInstalled { get; set; } = true;
    public AvailableUpdate? NextResult { get; set; }
    public Exception? ThrowOnCheck { get; set; }
    public int CheckCount { get; private set; }
    public int ApplyCount { get; private set; }

    public Task<AvailableUpdate?> CheckAndDownloadAsync()
    {
        CheckCount++;
        return ThrowOnCheck is not null
            ? Task.FromException<AvailableUpdate?>(ThrowOnCheck)
            : Task.FromResult(NextResult);
    }

    public void ApplyAndRestart() => ApplyCount++;
}
```
In `MainViewModelTests.MakeViewModel`, add the optional parameter `FakeUpdateService? updateService = null` and pass `updateService ?? new FakeUpdateService { IsInstalled = false }` as the new last constructor argument.

- [ ] **Step 2: Write the failing tests** (add them to `MainViewModelTests`)

```csharp
    private static AvailableUpdate Update398 => new("3.9.8", "- Fixed things.");

    [Fact]
    public async Task CheckForUpdatesOnStartup_WithUpdate_SetsReadyState()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.True(vm.IsUpdateReady);
        Assert.Equal("3.9.8", vm.UpdateReadyVersion);
        Assert.Equal("- Fixed things.", vm.UpdateReleaseNotes);
    }

    [Fact]
    public async Task CheckForUpdatesOnStartup_WhenNotInstalled_DoesNothing()
    {
        var updates = new FakeUpdateService { IsInstalled = false, NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.Equal(0, updates.CheckCount);
        Assert.False(vm.IsUpdateReady);
        Assert.Null(vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdatesOnStartup_SwallowsFailures()
    {
        var updates = new FakeUpdateService { ThrowOnCheck = new HttpRequestException("offline") };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.False(vm.IsUpdateReady);
        Assert.Null(vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdates_WhenNotInstalled_ExplainsWhy()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { IsInstalled = false });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Updates are only available in the installed version.", vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdates_WhenUpToDate_SaysSo()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService());

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal($"You're up to date (version {AppVersion.Current}).", vm.UpdateCheckMessage);
        Assert.False(vm.IsUpdateReady);
    }

    [Fact]
    public async Task CheckForUpdates_WithUpdate_ReportsReady()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { NextResult = Update398 });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Version 3.9.8 is ready — restart to update.", vm.UpdateCheckMessage);
        Assert.True(vm.IsUpdateReady);
    }

    [Fact]
    public async Task CheckForUpdates_OnFailure_ShowsTheError()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { ThrowOnCheck = new HttpRequestException("offline") });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't check for updates: offline", vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task TryRestartToUpdate_WithReadyUpdate_Applies()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);
        await vm.CheckForUpdatesOnStartupAsync();

        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(1, updates.ApplyCount);
    }

    [Fact]
    public async Task TryRestartToUpdate_DuringRefresh_NeedsConfirmation()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);
        await vm.CheckForUpdatesOnStartupAsync();
        vm.IsThiefGuildRefreshRunning = true;

        Assert.False(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(0, updates.ApplyCount);
        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: true));
        Assert.Equal(1, updates.ApplyCount);
    }

    [Fact]
    public void TryRestartToUpdate_WithoutUpdate_DoesNothing()
    {
        var updates = new FakeUpdateService();
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(0, updates.ApplyCount);
    }
```

- [ ] **Step 3: Run to verify failure**

Run: `dotnet test tests/ThiefManager.Tests --filter MainViewModelTests`
Expected: a build error, because the `MainViewModel` constructor doesn't take `IUpdateService` yet and the update members don't exist.

- [ ] **Step 4: Implement the view model**

In `MainViewModel`:
- Add the constructor parameter `IUpdateService updateService` last and store it in `_updateService`.
- In the constructor, add `CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);`.
- Add these members:
```csharp
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    /// <summary>The version of a downloaded update waiting for "Restart to update", or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateReady))]
    private string? updateReadyVersion;

    [ObservableProperty]
    private string? updateReleaseNotes;

    /// <summary>The result of a manual "Check for Updates", shown in the About window.</summary>
    [ObservableProperty]
    private string? updateCheckMessage;

    public bool IsUpdateReady => UpdateReadyVersion is not null;

    /// <summary>
    /// The quiet check on launch: skipped for dev (non-installed) runs, and any failure (offline,
    /// GitHub unavailable) is swallowed because an automatic check should never nag.
    /// </summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        if (!_updateService.IsInstalled)
            return;

        try
        {
            if (await _updateService.CheckAndDownloadAsync() is { } update)
                SetUpdateReady(update);
        }
        catch (Exception)
        {
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!_updateService.IsInstalled)
        {
            UpdateCheckMessage = "Updates are only available in the installed version.";
            return;
        }

        UpdateCheckMessage = "Checking for updates…";
        try
        {
            var update = await _updateService.CheckAndDownloadAsync();
            if (update is null)
            {
                UpdateCheckMessage = $"You're up to date (version {AppVersion.Current}).";
            }
            else
            {
                SetUpdateReady(update);
                UpdateCheckMessage = $"Version {update.Version} is ready — restart to update.";
            }
        }
        catch (Exception ex)
        {
            UpdateCheckMessage = $"Couldn't check for updates: {ex.Message}";
        }
    }

    private void SetUpdateReady(AvailableUpdate update)
    {
        UpdateReleaseNotes = update.ReleaseNotesMarkdown;
        UpdateReadyVersion = update.Version;
    }

    /// <summary>
    /// Applies a downloaded update and restarts. Returns false, without doing anything, when a
    /// Thief Guild refresh is running and the caller hasn't confirmed — the window then asks the
    /// user. With no update ready there's nothing to do and it returns true.
    /// </summary>
    public bool TryRestartToUpdate(bool confirmedDespiteRefresh)
    {
        if (!IsUpdateReady)
            return true;
        if (IsThiefGuildRefreshRunning && !confirmedDespiteRefresh)
            return false;

        _updateService.ApplyAndRestart();
        return true;
    }
```
`AppVersion` is in the parent namespace `ThiefManager`, so no `using` is needed.

- [ ] **Step 5: Implement the Velopack service, the startup hook and the wiring**

`src/ThiefManager/Services/VelopackUpdateService.cs`:
```csharp
using Velopack;
using Velopack.Sources;

namespace ThiefManager.Services;

/// <summary>Updates from this repo's public GitHub Releases, as published by .github/workflows/release.yml.</summary>
public class VelopackUpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/herokenshinmate/ThiefFMManager";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));
    private UpdateInfo? _downloaded;

    public bool IsInstalled => _manager.IsInstalled;

    public async Task<AvailableUpdate?> CheckAndDownloadAsync()
    {
        var info = await _manager.CheckForUpdatesAsync();
        if (info is null)
            return null;

        await _manager.DownloadUpdatesAsync(info);
        _downloaded = info;
        return new AvailableUpdate(info.TargetFullRelease.Version.ToString(), info.TargetFullRelease.NotesMarkdown);
    }

    public void ApplyAndRestart()
    {
        if (_downloaded is not null)
            _manager.ApplyUpdatesAndRestart(_downloaded);
    }
}
```
`src/ThiefManager/Program.cs`:
```csharp
using Velopack;

namespace ThiefManager;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before any WPF startup: it handles Velopack's install, update and uninstall
        // hooks, some of which exit the process immediately.
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```
`src/ThiefManager/ThiefManager.csproj`:
- Add `<StartupObject>ThiefManager.Program</StartupObject>` to the first `PropertyGroup`.
- Add `<PackageReference Include="Velopack" Version="1.2.158" />`.
- Add:
```xml
  <ItemGroup>
    <!-- Program.Main runs Velopack's hook before WPF, so App.xaml must not generate its own Main. -->
    <ApplicationDefinition Remove="App.xaml" />
    <Page Include="App.xaml" />
  </ItemGroup>
```

`src/ThiefManager/App.xaml.cs`:
- Create `var updateService = new VelopackUpdateService();` next to the other services.
- Pass it as the new last `MainViewModel` constructor argument.

- [ ] **Step 6: Run to verify pass**

Run `dotnet build src/ThiefManager`, expecting 0 warnings and 0 errors. Then run the filter from Step 3, then the full suite once.
Expected: all pass (254 + 10 = 264). Also check that `src/ThiefManager/bin/Debug/net8.0-windows/ThiefManager.dll` exists and that the build produced no `CS0017` (multiple entry points).

- [ ] **Step 7: Commit**

```bash
git add src/ThiefManager/Program.cs src/ThiefManager/Services/IUpdateService.cs src/ThiefManager/Services/VelopackUpdateService.cs src/ThiefManager/ThiefManager.csproj src/ThiefManager/ViewModels/MainViewModel.cs src/ThiefManager/App.xaml.cs tests/ThiefManager.Tests/Fakes/FakeUpdateService.cs tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs
git commit -m "Add Velopack startup hook and update checking to the view model"
```

---

### Task 3: Update UI: status bar notice, What's new, About window

**Files:**
- Modify:
  - `src/ThiefManager/MainWindow.xaml`
  - `src/ThiefManager/MainWindow.xaml.cs`
  - `src/ThiefManager/Views/AboutWindow.xaml`
  - `src/ThiefManager/Views/AboutWindow.xaml.cs`

**Interfaces:**
- Consumes these `MainViewModel` members from Task 2: `IsUpdateReady`, `UpdateReadyVersion`, `UpdateReleaseNotes`, `UpdateCheckMessage`, `CheckForUpdatesCommand`, `CheckForUpdatesOnStartupAsync()`, `TryRestartToUpdate(bool)` and `IsThiefGuildRefreshRunning`.

This task is XAML and code-behind only, so xUnit can't exercise it. Verify it by building, running the full test suite and reading the XAML carefully. **Don't launch the app.**

- [ ] **Step 1: Status bar notice (`MainWindow.xaml`)**

In the bottom status bar `DockPanel`, replace the `TextBlock` bound to `BackgroundStatus` with:
```xml
                <Grid>
                    <TextBlock Text="{Binding BackgroundStatus}" Foreground="{StaticResource MutedForegroundBrush}"
                               FontSize="11" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"
                               Visibility="{Binding IsUpdateReady, Converter={StaticResource InverseBoolToVisibilityConverter}}"/>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center"
                                Visibility="{Binding IsUpdateReady, Converter={StaticResource BoolToVisibilityConverter}}">
                        <ui:SymbolIcon Symbol="ArrowCircleUp24" Foreground="#FF3FB950" FontSize="14" Margin="0,0,6,0"/>
                        <TextBlock FontSize="12" VerticalAlignment="Center">
                            <Run Text="{Binding UpdateReadyVersion, Mode=OneWay, StringFormat='Version {0} is ready — '}"/><Hyperlink Click="RestartToUpdate_Click">Restart to update</Hyperlink><Run Text=" · "/><Hyperlink Click="WhatsNew_Click">What's new</Hyperlink>
                        </TextBlock>
                    </StackPanel>
                </Grid>
```

- [ ] **Step 2: Code-behind (`MainWindow.xaml.cs`)**

In the constructor's `Loaded` handler, after `await _viewModel.LoadCommand.ExecuteAsync(null);` and **before** the Thief Guild backfill call, add:
```csharp
            // Runs alongside the backfill; it swallows its own failures.
            _ = _viewModel.CheckForUpdatesOnStartupAsync();
```
Add these methods:
```csharp
    private async void RestartToUpdate_Click(object sender, RoutedEventArgs e) => await RestartToUpdateAsync();

    private async Task RestartToUpdateAsync()
    {
        try
        {
            if (_viewModel.TryRestartToUpdate(confirmedDespiteRefresh: false))
                return;

            var confirm = new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = "Restart to Update",
                Content = "A Thief Guild refresh is running. Restart to update anyway? It will resume next time.",
                PrimaryButtonText = "Restart",
                CloseButtonText = "Cancel"
            };
            if (await confirm.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
                _viewModel.TryRestartToUpdate(confirmedDespiteRefresh: true);
        }
        catch (Exception ex)
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Owner = this,
                Title = "Update Failed",
                Content = $"Couldn't apply the update: {ex.Message}",
                CloseButtonText = "OK"
            }.ShowDialogAsync();
        }
    }

    private async void WhatsNew_Click(object sender, RoutedEventArgs e)
    {
        var notes = string.IsNullOrWhiteSpace(_viewModel.UpdateReleaseNotes) ? "No release notes." : _viewModel.UpdateReleaseNotes;
        await new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = $"What's new in {_viewModel.UpdateReadyVersion}",
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new System.Windows.Controls.TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 }
            },
            CloseButtonText = "Close"
        }.ShowDialogAsync();
    }
```
Replace `OpenAbout_Click` with:
```csharp
    private void OpenAbout_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.UpdateCheckMessage = null;
        new AboutWindow(_viewModel, RestartToUpdateAsync) { Owner = this }.ShowDialog();
    }
```

- [ ] **Step 3: The About window**

`AboutWindow.xaml.cs`:
```csharp
using System.Windows;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class AboutWindow : FluentWindow
{
    private readonly Func<Task> _restartToUpdate;

    public AboutWindow(MainViewModel viewModel, Func<Task> restartToUpdate)
    {
        InitializeComponent();
        DataContext = viewModel;
        _restartToUpdate = restartToUpdate;
    }

    private async void RestartToUpdate_Click(object sender, RoutedEventArgs e) => await _restartToUpdate();
}
```
In `AboutWindow.xaml`, append the following inside the `StackPanel`, after the "Built with…" TextBlock:
```xml
            <ui:Button Content="Check for Updates" Command="{Binding CheckForUpdatesCommand}" HorizontalAlignment="Center" Margin="0,16,0,6">
                <ui:Button.Icon>
                    <ui:SymbolIcon Symbol="ArrowSync24"/>
                </ui:Button.Icon>
            </ui:Button>
            <TextBlock Text="{Binding UpdateCheckMessage}" TextWrapping="Wrap" TextAlignment="Center" MaxWidth="300"
                       FontSize="11" Foreground="{StaticResource MutedForegroundBrush}" HorizontalAlignment="Center"
                       Visibility="{Binding UpdateCheckMessage, Converter={StaticResource NullToVisibilityConverter}}"/>
            <ui:Button Content="Restart to Update" Click="RestartToUpdate_Click" Appearance="Success" HorizontalAlignment="Center" Margin="0,8,0,0"
                       Visibility="{Binding IsUpdateReady, Converter={StaticResource BoolToVisibilityConverter}}"/>
```

- [ ] **Step 4: Build, test and check the XAML carefully**

Run `dotnet build src/ThiefManager` (expect 0 errors and 0 warnings), then `dotnet test tests/ThiefManager.Tests` (expect 264 passing).

Then read the XAML carefully:
- The new bindings exist on `MainViewModel`.
- `InverseBoolToVisibilityConverter`, `BoolToVisibilityConverter` and `NullToVisibilityConverter` are App-level resources.
- `ArrowCircleUp24` is a valid symbol. It's confirmed present in WPF-UI 4.3.
- `Run Text` binds `OneWay`.
- Every `AboutWindow` construction site was updated.

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/MainWindow.xaml src/ThiefManager/MainWindow.xaml.cs src/ThiefManager/Views/AboutWindow.xaml src/ThiefManager/Views/AboutWindow.xaml.cs
git commit -m "Show update-ready notice, What's new and Check for Updates"
```

---

### Task 4: Release workflow, release docs and 3.9.7

**Files:**
- Create: `.github/workflows/release.yml`, `docs/RELEASING.md`
- Modify: `.gitignore`, `src/ThiefManager/AppVersion.cs`, `src/ThiefManager/ChangelogEntry.cs`

- [ ] **Step 1: The workflow**

`.github/workflows/release.yml`:
```yaml
name: Release

on:
  push:
    tags: ['v*.*.*']
  workflow_dispatch:
    inputs:
      tag:
        description: 'Existing tag to (re)release, e.g. v3.9.7'
        required: true

permissions:
  contents: write

env:
  REPO_URL: https://github.com/${{ github.repository }}
  VPK_VERSION: 1.2.158

jobs:
  release:
    runs-on: windows-latest
    steps:
      - name: Resolve tag and version
        id: tag
        shell: pwsh
        run: |
          $tag = if ('${{ github.event_name }}' -eq 'workflow_dispatch') { '${{ inputs.tag }}' } else { '${{ github.ref_name }}' }
          if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Tag '$tag' isn't in the form vX.Y.Z." }
          "tag=$tag" >> $env:GITHUB_OUTPUT
          "version=$($Matches[1])" >> $env:GITHUB_OUTPUT

      - uses: actions/checkout@v4
        with:
          ref: ${{ steps.tag.outputs.tag }}

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Verify version and extract release notes
        shell: pwsh
        run: ./build/Get-ReleaseNotes.ps1 -Version ${{ steps.tag.outputs.version }} -OutFile release-notes.md

      - name: Test
        run: dotnet test tests/ThiefManager.Tests -c Release

      - name: Publish (self-contained win-x64)
        run: dotnet publish src/ThiefManager -c Release -r win-x64 --self-contained -p:Version=${{ steps.tag.outputs.version }} -o publish

      - name: Install vpk
        run: dotnet tool install -g vpk --version ${{ env.VPK_VERSION }}

      # Fetches the previous release so vpk can build a small delta update. The very first release
      # has nothing to download, so a failure here is tolerated.
      - name: Download previous release
        shell: pwsh
        continue-on-error: true
        run: vpk download github --repoUrl ${{ env.REPO_URL }} --token ${{ secrets.GITHUB_TOKEN }} --outputDir releases

      - name: Pack installer and update packages
        shell: pwsh
        env:
          SIGN_PARAMS: ${{ secrets.SIGN_PARAMS }}
        run: |
          $vpkArgs = @(
            'pack',
            '--packId', 'ThiefFMManager',
            '--packVersion', '${{ steps.tag.outputs.version }}',
            '--packDir', 'publish',
            '--mainExe', 'ThiefManager.exe',
            '--packTitle', 'Thief FM Manager',
            '--icon', 'src/ThiefManager/Assets/AppIcon.ico',
            '--releaseNotes', 'release-notes.md',
            '--outputDir', 'releases'
          )
          if ($env:SIGN_PARAMS) {
            $vpkArgs += @('--signParams', $env:SIGN_PARAMS)
            Write-Host 'Signing enabled (SIGN_PARAMS secret is set).'
          } else {
            Write-Host 'Unsigned build: the SIGN_PARAMS secret is not set.'
          }
          vpk @vpkArgs
          if ($LASTEXITCODE -ne 0) { throw "vpk pack failed with exit code $LASTEXITCODE" }

      - name: Publish GitHub release
        shell: pwsh
        run: |
          vpk upload github --repoUrl ${{ env.REPO_URL }} --token ${{ secrets.GITHUB_TOKEN }} --publish --releaseName "Thief FM Manager ${{ steps.tag.outputs.version }}" --tag ${{ steps.tag.outputs.tag }} --outputDir releases
          if ($LASTEXITCODE -ne 0) { throw "vpk upload failed with exit code $LASTEXITCODE" }
```

- [ ] **Step 2: `docs/RELEASING.md`**

```markdown
# Releasing Thief FM Manager

Releases are built and published by GitHub Actions (`.github/workflows/release.yml`) when a version tag is pushed. The workflow tests the app, builds a self-contained Windows installer with [Velopack](https://velopack.io), and publishes a GitHub Release that installed copies update from.

## Cutting a release

1. Bump `AppVersion.Current` in `src/ThiefManager/AppVersion.cs`. Use a patch bump for normal changes, e.g. `3.9.7` → `3.9.8`.
2. Add a matching entry at the top of `ChangelogData.Entries` in `src/ThiefManager/ChangelogEntry.cs`. Its bullets become the GitHub release notes and the in-app "What's new" text.
3. Commit, and push to `main`.
4. Tag and push:
   ```
   git tag v3.9.8
   git push origin v3.9.8
   ```
5. Watch the **Release** workflow on the repository's Actions tab. When it finishes, the release appears under Releases with:
   - `ThiefFMManager-win-Setup.exe`: the installer for new users.
   - Full and delta `.nupkg` packages and the `releases.win.json` feed, which installed copies use to update.

The workflow stops before building if:
- the tag doesn't match `AppVersion.Current`, or the changelog has no entry for that version;
- any test fails.

To re-run a failed release without re-tagging, use **Run workflow** on the Release workflow and enter the existing tag.

You can check the version and changelog locally before tagging:

    powershell -File build/Get-ReleaseNotes.ps1 -Version 3.9.8

## Code signing (optional, currently off)

Builds are currently unsigned, so Windows SmartScreen warns on first install. To sign them, add a repository secret named `SIGN_PARAMS`, containing the `signtool.exe` arguments Velopack should use. For example:

    /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f cert.pfx /p <password>

The pack step then signs automatically. See `vpk pack --help` (`--signParams`) and the Velopack signing docs for other signing services.
```

- [ ] **Step 3: Ignore release artifacts, and bump to 3.9.7**

Append these lines to `.gitignore`:
```
publish/
releases/
release-notes.md
```
Change `AppVersion.Current` to `"3.9.7"`. Then add this entry at the top of `ChangelogData.Entries`:
```csharp
        new("3.9.7", "2026-09-25", new[]
        {
            "Thief FM Manager now has a proper installer and updates itself: new versions are downloaded in the background and a \"Restart to update\" link appears in the status bar when one is ready (see \"What's new\" for its changes).",
            "Added Check for Updates to the About window."
        }),
```

- [ ] **Step 4: Verify**

Run:
- `dotnet build src/ThiefManager`
- `dotnet test tests/ThiefManager.Tests`, expecting 264 passing. The release-notes script test now checks the 3.9.7 entry, including its escaped quotes.
- `powershell -NoProfile -File build/Get-ReleaseNotes.ps1 -Version 3.9.7 -OutFile $env:TEMP\rn.md`, which should exit 0 and print the path.

Also check the workflow YAML parses:
```
python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml')); print('ok')"
```
If PyYAML is missing, check the indentation carefully by reading the file instead.

Also run a local `dotnet publish src/ThiefManager -c Release -r win-x64 --self-contained -p:Version=3.9.7 -o <scratch dir outside the repo>`, and check that `ThiefManager.exe` exists in the output. Don't run it.

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/release.yml docs/RELEASING.md .gitignore src/ThiefManager/AppVersion.cs src/ThiefManager/ChangelogEntry.cs
git commit -m "Add release workflow and docs; bump to 3.9.7"
```
