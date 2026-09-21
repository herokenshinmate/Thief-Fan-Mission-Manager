# Thief Fan Mission Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF desktop app that catalogs Thief 1/2 fan missions with status/rating/notes, can scan FM folders for new missions, and can launch the configured game/loader.

**Architecture:** .NET 8 WPF app using MVVM (CommunityToolkit.Mvvm). Pure, unit-testable services (`ScanService`, `MissionQuery`, `LaunchService`) contain all logic that doesn't require WPF or a live database. SQLite (EF Core provider) is the storage layer, accessed through small repository classes behind interfaces so ViewModels can be tested with fakes. XAML views are thin and wire directly to ViewModel commands/properties.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm 8.x, Microsoft.EntityFrameworkCore.Sqlite 8.x, xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-09-21-thief-fm-manager-design.md`

## Global Constraints

- Windows-only desktop app (WPF) — no cross-platform requirement.
- Single local SQLite file for storage, no server component.
- v1 does not attempt to auto-select an FM inside FMSel's own UI — "Play" launches the configured executable only.
- All business logic (scanning, filtering/sorting, launch decisions) must be usable and testable without instantiating WPF or a real database file where feasible.

---

### Task 1: Solution & project scaffolding

**Files:**
- Create: `ThiefManager.sln`
- Create: `src/ThiefManager/ThiefManager.csproj`
- Create: `src/ThiefManager/App.xaml`, `src/ThiefManager/App.xaml.cs`
- Create: `src/ThiefManager/MainWindow.xaml`, `src/ThiefManager/MainWindow.xaml.cs`
- Create: `tests/ThiefManager.Tests/ThiefManager.Tests.csproj`
- Create: `.gitignore`

**Interfaces:**
- Produces: a buildable, runnable empty WPF app (`ThiefManager`) and an empty xUnit test project (`ThiefManager.Tests`) referencing it, wired into one solution.

- [ ] **Step 1: Initialize git and create the solution**

```bash
cd "G:/Projects_2026/ThiefManager"
git init
dotnet new sln -n ThiefManager
```

- [ ] **Step 2: Create the WPF project**

```bash
dotnet new wpf -n ThiefManager -o src/ThiefManager
dotnet sln add src/ThiefManager/ThiefManager.csproj
```

- [ ] **Step 3: Create the test project and reference the app project**

```bash
dotnet new xunit -n ThiefManager.Tests -o tests/ThiefManager.Tests
dotnet sln add tests/ThiefManager.Tests/ThiefManager.Tests.csproj
dotnet add tests/ThiefManager.Tests/ThiefManager.Tests.csproj reference src/ThiefManager/ThiefManager.csproj
```

Note: the WPF SDK (`Microsoft.NET.Sdk`) with `<UseWPF>true</UseWPF>` produces an `.exe`, which a normal test project can still reference for its types as long as `OutputType` isn't `WinExe`-only-restricted — this works fine with `dotnet add reference`. If the reference fails, add `<EnableDefaultItems>true</EnableDefaultItems>` is not needed; instead confirm `src/ThiefManager/ThiefManager.csproj` targets `net8.0-windows` and retry.

- [ ] **Step 4: Add package references needed by later tasks**

```bash
dotnet add src/ThiefManager/ThiefManager.csproj package CommunityToolkit.Mvvm
dotnet add src/ThiefManager/ThiefManager.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

- [ ] **Step 5: Add `.gitignore`**

```
bin/
obj/
*.user
*.db
```

- [ ] **Step 6: Build to verify scaffolding works**

Run: `dotnet build`
Expected: Build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Scaffold ThiefManager WPF app and test project"
```

---

### Task 2: Domain models and enums

**Files:**
- Create: `src/ThiefManager/Models/GameTitle.cs`
- Create: `src/ThiefManager/Models/MissionStatus.cs`
- Create: `src/ThiefManager/Models/FanMission.cs`
- Create: `src/ThiefManager/Models/AppSettings.cs`
- Test: `tests/ThiefManager.Tests/Models/FanMissionTests.cs`

**Interfaces:**
- Produces: `GameTitle { Thief1, Thief2 }`, `MissionStatus { NotPlayed, InProgress, Completed, Abandoned }`, `FanMission` (Id:int, Title:string, Game:GameTitle, Author:string?, ReleaseYear:int?, Status:MissionStatus, Rating:int?, Tags:string, Notes:string?, DateStarted:DateTime?, DateCompleted:DateTime?, FolderPath:string), `AppSettings` (Id:int, Thief1FmFolder:string?, Thief2FmFolder:string?, Thief1ExePath:string?, Thief2ExePath:string?). These are consumed by every later task.

- [ ] **Step 1: Write the failing test for default values**

```csharp
// tests/ThiefManager.Tests/Models/FanMissionTests.cs
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Models;

public class FanMissionTests
{
    [Fact]
    public void NewFanMission_DefaultsToNotPlayedWithEmptyTags()
    {
        var mission = new FanMission();

        Assert.Equal(MissionStatus.NotPlayed, mission.Status);
        Assert.Equal(string.Empty, mission.Tags);
        Assert.Equal(string.Empty, mission.Title);
        Assert.Equal(string.Empty, mission.FolderPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ThiefManager.Tests --filter FanMissionTests`
Expected: FAIL (compile error — `ThiefManager.Models` doesn't exist yet).

- [ ] **Step 3: Create the enums**

```csharp
// src/ThiefManager/Models/GameTitle.cs
namespace ThiefManager.Models;

public enum GameTitle
{
    Thief1,
    Thief2
}
```

```csharp
// src/ThiefManager/Models/MissionStatus.cs
namespace ThiefManager.Models;

public enum MissionStatus
{
    NotPlayed,
    InProgress,
    Completed,
    Abandoned
}
```

- [ ] **Step 4: Create the `FanMission` and `AppSettings` models**

```csharp
// src/ThiefManager/Models/FanMission.cs
namespace ThiefManager.Models;

public class FanMission
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public GameTitle Game { get; set; }
    public string? Author { get; set; }
    public int? ReleaseYear { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.NotPlayed;
    public int? Rating { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public string FolderPath { get; set; } = string.Empty;
}
```

```csharp
// src/ThiefManager/Models/AppSettings.cs
namespace ThiefManager.Models;

public class AppSettings
{
    public int Id { get; set; }
    public string? Thief1FmFolder { get; set; }
    public string? Thief2FmFolder { get; set; }
    public string? Thief1ExePath { get; set; }
    public string? Thief2ExePath { get; set; }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/ThiefManager.Tests --filter FanMissionTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/Models tests/ThiefManager.Tests/Models
git commit -m "Add FanMission and AppSettings domain models"
```

---

### Task 3: ScanService (pure folder-scan logic)

**Files:**
- Create: `src/ThiefManager/Services/ScanCandidate.cs`
- Create: `src/ThiefManager/Services/ScanService.cs`
- Test: `tests/ThiefManager.Tests/Services/ScanServiceTests.cs`

**Interfaces:**
- Consumes: nothing beyond BCL (`System.IO.Path`).
- Produces: `record ScanCandidate(string SuggestedTitle, string FolderPath)`; `static class ScanService { static IReadOnlyList<ScanCandidate> FindNewCandidates(IEnumerable<string> subfolderPaths, IEnumerable<string> existingFolderPaths) }`. Consumed by the future `ScanViewModel` (Task 11).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ThiefManager.Tests/Services/ScanServiceTests.cs
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ScanServiceTests
{
    [Fact]
    public void FindNewCandidates_ExcludesFoldersAlreadyCataloged()
    {
        var subfolders = new[]
        {
            @"C:\fms\MissionA",
            @"C:\fms\MissionB"
        };
        var existing = new[] { @"C:\fms\MissionA" };

        var result = ScanService.FindNewCandidates(subfolders, existing);

        var candidate = Assert.Single(result);
        Assert.Equal(@"C:\fms\MissionB", candidate.FolderPath);
        Assert.Equal("MissionB", candidate.SuggestedTitle);
    }

    [Fact]
    public void FindNewCandidates_ComparisonIsCaseInsensitive()
    {
        var subfolders = new[] { @"C:\fms\MissionA" };
        var existing = new[] { @"c:\fms\missiona" };

        var result = ScanService.FindNewCandidates(subfolders, existing);

        Assert.Empty(result);
    }

    [Fact]
    public void FindNewCandidates_TrimsTrailingSlashWhenNamingSuggestedTitle()
    {
        var subfolders = new[] { @"C:\fms\MissionC\" };

        var result = ScanService.FindNewCandidates(subfolders, Array.Empty<string>());

        Assert.Equal("MissionC", Assert.Single(result).SuggestedTitle);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter ScanServiceTests`
Expected: FAIL (compile error — `ThiefManager.Services` doesn't exist yet).

- [ ] **Step 3: Implement `ScanCandidate` and `ScanService`**

```csharp
// src/ThiefManager/Services/ScanCandidate.cs
namespace ThiefManager.Services;

public record ScanCandidate(string SuggestedTitle, string FolderPath);
```

```csharp
// src/ThiefManager/Services/ScanService.cs
namespace ThiefManager.Services;

public static class ScanService
{
    public static IReadOnlyList<ScanCandidate> FindNewCandidates(
        IEnumerable<string> subfolderPaths,
        IEnumerable<string> existingFolderPaths)
    {
        var existing = new HashSet<string>(existingFolderPaths, StringComparer.OrdinalIgnoreCase);

        return subfolderPaths
            .Where(path => !existing.Contains(path))
            .Select(path =>
            {
                var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var title = Path.GetFileName(trimmed);
                return new ScanCandidate(title, path);
            })
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter ScanServiceTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/ScanCandidate.cs src/ThiefManager/Services/ScanService.cs tests/ThiefManager.Tests/Services/ScanServiceTests.cs
git commit -m "Add ScanService for detecting new fan mission folders"
```

---

### Task 4: MissionQuery (filter/sort logic)

**Files:**
- Create: `src/ThiefManager/Services/SortField.cs`
- Create: `src/ThiefManager/Services/MissionQuery.cs`
- Test: `tests/ThiefManager.Tests/Services/MissionQueryTests.cs`

**Interfaces:**
- Consumes: `FanMission`, `GameTitle`, `MissionStatus` (Task 2).
- Produces: `enum SortField { Title, Game, Status, Rating }`; `static class MissionQuery { static IEnumerable<FanMission> Apply(IEnumerable<FanMission> missions, GameTitle? gameFilter, MissionStatus? statusFilter, string? tagFilter, SortField sortField, bool ascending) }`. Consumed by `MainViewModel` (Task 8).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ThiefManager.Tests/Services/MissionQueryTests.cs
using ThiefManager.Models;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class MissionQueryTests
{
    private static FanMission Mission(string title, GameTitle game, MissionStatus status, int? rating, string tags = "") =>
        new()
        {
            Title = title,
            Game = game,
            Status = status,
            Rating = rating,
            Tags = tags
        };

    [Fact]
    public void Apply_FiltersByGame()
    {
        var missions = new[]
        {
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null),
            Mission("B", GameTitle.Thief2, MissionStatus.NotPlayed, null)
        };

        var result = MissionQuery.Apply(missions, GameTitle.Thief2, null, null, SortField.Title, true);

        Assert.Equal(new[] { "B" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_FiltersByTagCaseInsensitively()
    {
        var missions = new[]
        {
            Mission("A", GameTitle.Thief1, MissionStatus.NotPlayed, null, "Horror, Heist"),
            Mission("B", GameTitle.Thief1, MissionStatus.NotPlayed, null, "Puzzle")
        };

        var result = MissionQuery.Apply(missions, null, null, "horror", SortField.Title, true);

        Assert.Equal(new[] { "A" }, result.Select(m => m.Title));
    }

    [Fact]
    public void Apply_SortsByRatingDescendingWithUnratedLast()
    {
        var missions = new[]
        {
            Mission("Unrated", GameTitle.Thief1, MissionStatus.NotPlayed, null),
            Mission("High", GameTitle.Thief1, MissionStatus.Completed, 5),
            Mission("Low", GameTitle.Thief1, MissionStatus.Completed, 2)
        };

        var result = MissionQuery.Apply(missions, null, null, null, SortField.Rating, false);

        Assert.Equal(new[] { "High", "Low", "Unrated" }, result.Select(m => m.Title));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionQueryTests`
Expected: FAIL (compile error — `SortField`/`MissionQuery` don't exist yet).

- [ ] **Step 3: Implement `SortField` and `MissionQuery`**

```csharp
// src/ThiefManager/Services/SortField.cs
namespace ThiefManager.Services;

public enum SortField
{
    Title,
    Game,
    Status,
    Rating
}
```

```csharp
// src/ThiefManager/Services/MissionQuery.cs
using ThiefManager.Models;

namespace ThiefManager.Services;

public static class MissionQuery
{
    public static IEnumerable<FanMission> Apply(
        IEnumerable<FanMission> missions,
        GameTitle? gameFilter,
        MissionStatus? statusFilter,
        string? tagFilter,
        SortField sortField,
        bool ascending)
    {
        var query = missions.AsEnumerable();

        if (gameFilter.HasValue)
            query = query.Where(m => m.Game == gameFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(m => m.Status == statusFilter.Value);

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            query = query.Where(m => m.Tags
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(tag => tag.Equals(tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        object KeySelector(FanMission m) => sortField switch
        {
            SortField.Title => m.Title,
            SortField.Game => m.Game,
            SortField.Status => m.Status,
            SortField.Rating => m.Rating ?? -1,
            _ => m.Title
        };

        query = ascending ? query.OrderBy(KeySelector) : query.OrderByDescending(KeySelector);

        return query.ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionQueryTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/SortField.cs src/ThiefManager/Services/MissionQuery.cs tests/ThiefManager.Tests/Services/MissionQueryTests.cs
git commit -m "Add MissionQuery for filtering and sorting the catalog"
```

---

### Task 5: LaunchService

**Files:**
- Create: `src/ThiefManager/Services/IProcessLauncher.cs`
- Create: `src/ThiefManager/Services/ProcessLauncher.cs`
- Create: `src/ThiefManager/Services/IFileExistsChecker.cs`
- Create: `src/ThiefManager/Services/FileExistsChecker.cs`
- Create: `src/ThiefManager/Services/LaunchResult.cs`
- Create: `src/ThiefManager/Services/LaunchService.cs`
- Test: `tests/ThiefManager.Tests/Services/LaunchServiceTests.cs`

**Interfaces:**
- Consumes: nothing beyond BCL.
- Produces: `interface IProcessLauncher { void Start(string exePath); }`; `interface IFileExistsChecker { bool Exists(string path); }`; `record LaunchResult(bool Ok, string? Error)` with `LaunchResult.Success()` / `LaunchResult.Failure(string)`; `class LaunchService(IProcessLauncher launcher, IFileExistsChecker fileExistsChecker) { LaunchResult Launch(string? exePath) }`. `ProcessLauncher`/`FileExistsChecker` are the real implementations wired in `App.xaml.cs` (Task 13); `MissionListViewModel` (Task 8) consumes `LaunchService`.

- [ ] **Step 1: Write the failing tests using fakes**

```csharp
// tests/ThiefManager.Tests/Services/LaunchServiceTests.cs
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class FakeProcessLauncher : IProcessLauncher
{
    public string? LastStartedPath { get; private set; }
    public void Start(string exePath) => LastStartedPath = exePath;
}

public class FakeFileExistsChecker : IFileExistsChecker
{
    private readonly HashSet<string> _existingPaths;
    public FakeFileExistsChecker(params string[] existingPaths) => _existingPaths = new HashSet<string>(existingPaths);
    public bool Exists(string path) => _existingPaths.Contains(path);
}

public class LaunchServiceTests
{
    [Fact]
    public void Launch_WithValidConfiguredPath_StartsProcessAndSucceeds()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker(@"C:\Games\Thief2\Thief2.exe"));

        var result = service.Launch(@"C:\Games\Thief2\Thief2.exe");

        Assert.True(result.Ok);
        Assert.Equal(@"C:\Games\Thief2\Thief2.exe", launcher.LastStartedPath);
    }

    [Fact]
    public void Launch_WithNullPath_FailsWithoutStartingProcess()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker());

        var result = service.Launch(null);

        Assert.False(result.Ok);
        Assert.Null(launcher.LastStartedPath);
        Assert.Contains("Settings", result.Error);
    }

    [Fact]
    public void Launch_WithPathThatDoesNotExist_FailsWithoutStartingProcess()
    {
        var launcher = new FakeProcessLauncher();
        var service = new LaunchService(launcher, new FakeFileExistsChecker());

        var result = service.Launch(@"C:\missing.exe");

        Assert.False(result.Ok);
        Assert.Null(launcher.LastStartedPath);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter LaunchServiceTests`
Expected: FAIL (compile error — types don't exist yet).

- [ ] **Step 3: Implement the interfaces, real implementations, and `LaunchService`**

```csharp
// src/ThiefManager/Services/IProcessLauncher.cs
namespace ThiefManager.Services;

public interface IProcessLauncher
{
    void Start(string exePath);
}
```

```csharp
// src/ThiefManager/Services/ProcessLauncher.cs
using System.Diagnostics;

namespace ThiefManager.Services;

public class ProcessLauncher : IProcessLauncher
{
    public void Start(string exePath) =>
        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
}
```

```csharp
// src/ThiefManager/Services/IFileExistsChecker.cs
namespace ThiefManager.Services;

public interface IFileExistsChecker
{
    bool Exists(string path);
}
```

```csharp
// src/ThiefManager/Services/FileExistsChecker.cs
namespace ThiefManager.Services;

public class FileExistsChecker : IFileExistsChecker
{
    public bool Exists(string path) => File.Exists(path);
}
```

```csharp
// src/ThiefManager/Services/LaunchResult.cs
namespace ThiefManager.Services;

public record LaunchResult(bool Ok, string? Error)
{
    public static LaunchResult Success() => new(true, null);
    public static LaunchResult Failure(string error) => new(false, error);
}
```

```csharp
// src/ThiefManager/Services/LaunchService.cs
namespace ThiefManager.Services;

public class LaunchService
{
    private readonly IProcessLauncher _launcher;
    private readonly IFileExistsChecker _fileExistsChecker;

    public LaunchService(IProcessLauncher launcher, IFileExistsChecker fileExistsChecker)
    {
        _launcher = launcher;
        _fileExistsChecker = fileExistsChecker;
    }

    public LaunchResult Launch(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !_fileExistsChecker.Exists(exePath))
            return LaunchResult.Failure("No valid executable is configured for this game. Set it in Settings.");

        _launcher.Start(exePath);
        return LaunchResult.Success();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter LaunchServiceTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/IProcessLauncher.cs src/ThiefManager/Services/ProcessLauncher.cs src/ThiefManager/Services/IFileExistsChecker.cs src/ThiefManager/Services/FileExistsChecker.cs src/ThiefManager/Services/LaunchResult.cs src/ThiefManager/Services/LaunchService.cs tests/ThiefManager.Tests/Services/LaunchServiceTests.cs
git commit -m "Add LaunchService for starting the configured game executable"
```

---

### Task 6: SQLite DbContext

**Files:**
- Create: `src/ThiefManager/Data/ThiefManagerDbContext.cs`
- Test: `tests/ThiefManager.Tests/Data/ThiefManagerDbContextTests.cs`

**Interfaces:**
- Consumes: `FanMission`, `AppSettings` (Task 2).
- Produces: `class ThiefManagerDbContext(string dbPath) : DbContext { DbSet<FanMission> FanMissions; DbSet<AppSettings> Settings; }`. Consumed by repositories (Task 7).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/ThiefManager.Tests/Data/ThiefManagerDbContextTests.cs
using Microsoft.EntityFrameworkCore;
using ThiefManager.Data;
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Data;

public class ThiefManagerDbContextTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"thiefmanager-test-{Guid.NewGuid()}.db");

    [Fact]
    public void EnsureCreated_ThenSaveAndReadFanMission_RoundTrips()
    {
        using (var db = new ThiefManagerDbContext(_dbPath))
        {
            db.Database.EnsureCreated();
            db.FanMissions.Add(new FanMission { Title = "Thief's Den", Game = GameTitle.Thief1, FolderPath = @"C:\fms\ThiefsDen" });
            db.SaveChanges();
        }

        using (var db = new ThiefManagerDbContext(_dbPath))
        {
            var saved = db.FanMissions.Single();
            Assert.Equal("Thief's Den", saved.Title);
            Assert.Equal(GameTitle.Thief1, saved.Game);
        }
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/ThiefManager.Tests --filter ThiefManagerDbContextTests`
Expected: FAIL (compile error — `ThiefManager.Data` doesn't exist yet).

- [ ] **Step 3: Add the EF Core Sqlite package to the test project and implement the context**

```bash
dotnet add tests/ThiefManager.Tests/ThiefManager.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

```csharp
// src/ThiefManager/Data/ThiefManagerDbContext.cs
using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class ThiefManagerDbContext : DbContext
{
    private readonly string _dbPath;

    public ThiefManagerDbContext(string dbPath) => _dbPath = dbPath;

    public DbSet<FanMission> FanMissions => Set<FanMission>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/ThiefManager.Tests --filter ThiefManagerDbContextTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Data/ThiefManagerDbContext.cs tests/ThiefManager.Tests/Data/ThiefManagerDbContextTests.cs tests/ThiefManager.Tests/ThiefManager.Tests.csproj
git commit -m "Add SQLite-backed ThiefManagerDbContext"
```

---

### Task 7: Repositories

**Files:**
- Create: `src/ThiefManager/Data/IMissionRepository.cs`
- Create: `src/ThiefManager/Data/MissionRepository.cs`
- Create: `src/ThiefManager/Data/ISettingsRepository.cs`
- Create: `src/ThiefManager/Data/SettingsRepository.cs`
- Test: `tests/ThiefManager.Tests/Data/MissionRepositoryTests.cs`
- Test: `tests/ThiefManager.Tests/Data/SettingsRepositoryTests.cs`

**Interfaces:**
- Consumes: `ThiefManagerDbContext` (Task 6), `FanMission`, `AppSettings` (Task 2).
- Produces: `interface IMissionRepository { Task<List<FanMission>> GetAllAsync(); Task AddAsync(FanMission mission); Task UpdateAsync(FanMission mission); Task DeleteAsync(int id); }` and `MissionRepository(Func<ThiefManagerDbContext> contextFactory) : IMissionRepository`; `interface ISettingsRepository { Task<AppSettings> GetAsync(); Task SaveAsync(AppSettings settings); }` and `SettingsRepository(Func<ThiefManagerDbContext> contextFactory) : ISettingsRepository`. Consumed by ViewModels (Tasks 8-11).

- [ ] **Step 1: Write the failing repository tests**

```csharp
// tests/ThiefManager.Tests/Data/MissionRepositoryTests.cs
using ThiefManager.Data;
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Data;

public class MissionRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"thiefmanager-test-{Guid.NewGuid()}.db");
    private ThiefManagerDbContext CreateContext() => new(_dbPath);

    public MissionRepositoryTests()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_ReturnsTheMission()
    {
        var repo = new MissionRepository(CreateContext);

        await repo.AddAsync(new FanMission { Title = "A New Job", Game = GameTitle.Thief2, FolderPath = @"C:\fms\ANewJob" });
        var all = await repo.GetAllAsync();

        Assert.Equal("A New Job", Assert.Single(all).Title);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedFields()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "Original", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Orig" });
        var saved = (await repo.GetAllAsync()).Single();

        saved.Rating = 5;
        saved.Status = MissionStatus.Completed;
        await repo.UpdateAsync(saved);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(5, reloaded.Rating);
        Assert.Equal(MissionStatus.Completed, reloaded.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheMission()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "ToDelete", Game = GameTitle.Thief1, FolderPath = @"C:\fms\ToDelete" });
        var saved = (await repo.GetAllAsync()).Single();

        await repo.DeleteAsync(saved.Id);

        Assert.Empty(await repo.GetAllAsync());
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
```

```csharp
// tests/ThiefManager.Tests/Data/SettingsRepositoryTests.cs
using ThiefManager.Data;
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Data;

public class SettingsRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"thiefmanager-test-{Guid.NewGuid()}.db");
    private ThiefManagerDbContext CreateContext() => new(_dbPath);

    public SettingsRepositoryTests()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetAsync_WithNoSavedSettings_ReturnsDefaultRow()
    {
        var repo = new SettingsRepository(CreateContext);

        var settings = await repo.GetAsync();

        Assert.Null(settings.Thief1ExePath);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsValues()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.Thief1ExePath = @"C:\Games\Thief1\Thief.exe";
        settings.Thief1FmFolder = @"C:\Games\Thief1\fms";

        await repo.SaveAsync(settings);
        var reloaded = await repo.GetAsync();

        Assert.Equal(@"C:\Games\Thief1\Thief.exe", reloaded.Thief1ExePath);
        Assert.Equal(@"C:\Games\Thief1\fms", reloaded.Thief1FmFolder);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter "MissionRepositoryTests|SettingsRepositoryTests"`
Expected: FAIL (compile error — repository types don't exist yet).

- [ ] **Step 3: Implement the repository interfaces and classes**

```csharp
// src/ThiefManager/Data/IMissionRepository.cs
using ThiefManager.Models;

namespace ThiefManager.Data;

public interface IMissionRepository
{
    Task<List<FanMission>> GetAllAsync();
    Task AddAsync(FanMission mission);
    Task UpdateAsync(FanMission mission);
    Task DeleteAsync(int id);
}
```

```csharp
// src/ThiefManager/Data/MissionRepository.cs
using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class MissionRepository : IMissionRepository
{
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public MissionRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<List<FanMission>> GetAllAsync()
    {
        using var db = _contextFactory();
        return await db.FanMissions.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(FanMission mission)
    {
        using var db = _contextFactory();
        db.FanMissions.Add(mission);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FanMission mission)
    {
        using var db = _contextFactory();
        db.FanMissions.Update(mission);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _contextFactory();
        var entity = await db.FanMissions.FindAsync(id);
        if (entity is null)
            return;

        db.FanMissions.Remove(entity);
        await db.SaveChangesAsync();
    }
}
```

```csharp
// src/ThiefManager/Data/ISettingsRepository.cs
using ThiefManager.Models;

namespace ThiefManager.Data;

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}
```

```csharp
// src/ThiefManager/Data/SettingsRepository.cs
using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class SettingsRepository : ISettingsRepository
{
    private const int SingletonId = 1;
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public SettingsRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<AppSettings> GetAsync()
    {
        using var db = _contextFactory();
        var existing = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SingletonId);
        return existing ?? new AppSettings { Id = SingletonId };
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings.Id = SingletonId;
        using var db = _contextFactory();
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Id == SingletonId);
        if (existing is null)
        {
            db.Settings.Add(settings);
        }
        else
        {
            existing.Thief1FmFolder = settings.Thief1FmFolder;
            existing.Thief2FmFolder = settings.Thief2FmFolder;
            existing.Thief1ExePath = settings.Thief1ExePath;
            existing.Thief2ExePath = settings.Thief2ExePath;
        }
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter "MissionRepositoryTests|SettingsRepositoryTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Data tests/ThiefManager.Tests/Data
git commit -m "Add mission and settings repositories"
```

---

### Task 8: MainViewModel (catalog list, filter, sort, launch, delete)

**Files:**
- Create: `src/ThiefManager/ViewModels/MainViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs`
- Test helper: `tests/ThiefManager.Tests/Fakes/FakeMissionRepository.cs`

**Interfaces:**
- Consumes: `IMissionRepository` (Task 7), `MissionQuery`, `SortField` (Task 4), `LaunchService` (Task 5), `FanMission`, `GameTitle`, `MissionStatus` (Task 2).
- Produces: `class MainViewModel(IMissionRepository missionRepository, LaunchService launchService)` : `ObservableObject` with `ObservableCollection<FanMission> VisibleMissions`, `GameTitle? GameFilter`, `MissionStatus? StatusFilter`, `string? TagFilter`, `SortField SortField`, `bool SortAscending`, `FanMission? SelectedMission`, `string? LaunchError`, `IAsyncRelayCommand LoadCommand`, `IRelayCommand LaunchSelectedCommand`, `IAsyncRelayCommand DeleteSelectedCommand`. Consumed by `MainWindow.xaml` (Task 12).

- [ ] **Step 1: Add a fake repository test helper**

```csharp
// tests/ThiefManager.Tests/Fakes/FakeMissionRepository.cs
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeMissionRepository : IMissionRepository
{
    public List<FanMission> Missions { get; } = new();

    public Task<List<FanMission>> GetAllAsync() => Task.FromResult(Missions.ToList());

    public Task AddAsync(FanMission mission)
    {
        mission.Id = Missions.Count == 0 ? 1 : Missions.Max(m => m.Id) + 1;
        Missions.Add(mission);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FanMission mission)
    {
        var index = Missions.FindIndex(m => m.Id == mission.Id);
        if (index >= 0)
            Missions[index] = mission;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        Missions.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing ViewModel tests**

```csharp
// tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs
using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class MainViewModelTests
{
    private static LaunchService MakeLaunchService(bool exeExists) =>
        new(new RecordingProcessLauncher(), new StubFileExistsChecker(exeExists));

    private class RecordingProcessLauncher : IProcessLauncher
    {
        public string? LastStartedPath;
        public void Start(string exePath) => LastStartedPath = exePath;
    }

    private class StubFileExistsChecker : IFileExistsChecker
    {
        private readonly bool _exists;
        public StubFileExistsChecker(bool exists) => _exists = exists;
        public bool Exists(string path) => _exists;
    }

    [Fact]
    public async Task LoadCommand_PopulatesVisibleMissionsFromRepository()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Z", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "A", Game = GameTitle.Thief1, FolderPath = "p2" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "A", "Z" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task SettingGameFilter_NarrowsVisibleMissions()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "T1 Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "T2 Mission", Game = GameTitle.Thief2, FolderPath = "p2" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.GameFilter = GameTitle.Thief2;

        Assert.Equal(new[] { "T2 Mission" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task LaunchSelectedCommand_WithNoExeConfigured_SetsLaunchError()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = new MainViewModel(repo, MakeLaunchService(false));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.LaunchSelectedCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.LaunchError));
    }

    [Fact]
    public async Task DeleteSelectedCommand_RemovesMissionFromRepositoryAndList()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(vm.VisibleMissions);
        Assert.Empty(await repo.GetAllAsync());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter MainViewModelTests`
Expected: FAIL (compile error — `ThiefManager.ViewModels.MainViewModel` doesn't exist yet).

- [ ] **Step 4: Implement `MainViewModel`**

```csharp
// src/ThiefManager/ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMissionRepository _missionRepository;
    private readonly LaunchService _launchService;
    private List<FanMission> _allMissions = new();

    public MainViewModel(IMissionRepository missionRepository, LaunchService launchService)
    {
        _missionRepository = missionRepository;
        _launchService = launchService;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LaunchSelectedCommand = new RelayCommand(LaunchSelected, () => SelectedMission is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedMission is not null);
    }

    public ObservableCollection<FanMission> VisibleMissions { get; } = new();

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand LaunchSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }

    [ObservableProperty]
    private GameTitle? gameFilter;

    [ObservableProperty]
    private MissionStatus? statusFilter;

    [ObservableProperty]
    private string? tagFilter;

    [ObservableProperty]
    private SortField sortField = SortField.Title;

    [ObservableProperty]
    private bool sortAscending = true;

    [ObservableProperty]
    private FanMission? selectedMission;

    [ObservableProperty]
    private string? launchError;

    partial void OnGameFilterChanged(GameTitle? value) => ApplyQuery();
    partial void OnStatusFilterChanged(MissionStatus? value) => ApplyQuery();
    partial void OnTagFilterChanged(string? value) => ApplyQuery();
    partial void OnSortFieldChanged(SortField value) => ApplyQuery();
    partial void OnSortAscendingChanged(bool value) => ApplyQuery();

    partial void OnSelectedMissionChanged(FanMission? value)
    {
        LaunchSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        _allMissions = await _missionRepository.GetAllAsync();
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        var filtered = MissionQuery.Apply(_allMissions, GameFilter, StatusFilter, TagFilter, SortField, SortAscending);
        VisibleMissions.Clear();
        foreach (var mission in filtered)
            VisibleMissions.Add(mission);
    }

    private void LaunchSelected()
    {
        if (SelectedMission is null)
            return;

        var exePath = SelectedMission.Game == GameTitle.Thief1 ? _thief1ExePath : _thief2ExePath;
        var result = _launchService.Launch(exePath);
        LaunchError = result.Ok ? null : result.Error;
    }

    private string? _thief1ExePath;
    private string? _thief2ExePath;

    public void ConfigureExePaths(string? thief1ExePath, string? thief2ExePath)
    {
        _thief1ExePath = thief1ExePath;
        _thief2ExePath = thief2ExePath;
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedMission is null)
            return;

        await _missionRepository.DeleteAsync(SelectedMission.Id);
        _allMissions.RemoveAll(m => m.Id == SelectedMission.Id);
        SelectedMission = null;
        ApplyQuery();
    }
}
```

Note: `ConfigureExePaths` is called by the composition root (Task 13) after settings load, and again whenever `SettingsViewModel` (Task 10) saves new paths.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter MainViewModelTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/ViewModels/MainViewModel.cs tests/ThiefManager.Tests/ViewModels/MainViewModelTests.cs tests/ThiefManager.Tests/Fakes/FakeMissionRepository.cs
git commit -m "Add MainViewModel with filtering, sorting, launch, and delete"
```

---

### Task 9: MissionEditViewModel (add/edit a mission)

**Files:**
- Create: `src/ThiefManager/ViewModels/MissionEditViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/MissionEditViewModelTests.cs`

**Interfaces:**
- Consumes: `IMissionRepository` (Task 7), `FanMission`, `GameTitle`, `MissionStatus` (Task 2).
- Produces: `class MissionEditViewModel(IMissionRepository missionRepository)` : `ObservableObject` with settable properties mirroring `FanMission` fields (`Title`, `Game`, `Author`, `ReleaseYear`, `Status`, `Rating`, `Tags`, `Notes`, `DateStarted`, `DateCompleted`, `FolderPath`), `void LoadFrom(FanMission mission)` (for editing; leave defaults for "add new"), `IAsyncRelayCommand SaveCommand`, `event EventHandler? Saved`. Consumed by `MainWindow.xaml`'s edit panel (Task 12).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ThiefManager.Tests/ViewModels/MissionEditViewModelTests.cs
using ThiefManager.Models;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class MissionEditViewModelTests
{
    [Fact]
    public async Task SaveCommand_WithNoLoadedMission_AddsNewMissionToRepository()
    {
        var repo = new FakeMissionRepository();
        var vm = new MissionEditViewModel(repo)
        {
            Title = "New Mission",
            Game = GameTitle.Thief2,
            FolderPath = @"C:\fms\NewMission"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Equal("New Mission", saved.Title);
        Assert.Equal(GameTitle.Thief2, saved.Game);
    }

    [Fact]
    public async Task SaveCommand_AfterLoadFrom_UpdatesExistingMission()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Original", Game = GameTitle.Thief1, FolderPath = "p1" });
        var existing = repo.Missions.Single();
        var vm = new MissionEditViewModel(repo);
        vm.LoadFrom(existing);

        vm.Rating = 4;
        vm.Status = MissionStatus.Completed;
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Equal(4, saved.Rating);
        Assert.Equal(MissionStatus.Completed, saved.Status);
        Assert.Equal("Original", saved.Title);
    }

    [Fact]
    public async Task SaveCommand_RaisesSavedEvent()
    {
        var repo = new FakeMissionRepository();
        var vm = new MissionEditViewModel(repo) { Title = "X", FolderPath = "p" };
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionEditViewModelTests`
Expected: FAIL (compile error — `MissionEditViewModel` doesn't exist yet).

- [ ] **Step 3: Implement `MissionEditViewModel`**

```csharp
// src/ThiefManager/ViewModels/MissionEditViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class MissionEditViewModel : ObservableObject
{
    private readonly IMissionRepository _missionRepository;
    private int _id;

    public MissionEditViewModel(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public event EventHandler? Saved;

    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private GameTitle game;
    [ObservableProperty] private string? author;
    [ObservableProperty] private int? releaseYear;
    [ObservableProperty] private MissionStatus status = MissionStatus.NotPlayed;
    [ObservableProperty] private int? rating;
    [ObservableProperty] private string tags = string.Empty;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private DateTime? dateStarted;
    [ObservableProperty] private DateTime? dateCompleted;
    [ObservableProperty] private string folderPath = string.Empty;

    public void LoadFrom(FanMission mission)
    {
        _id = mission.Id;
        Title = mission.Title;
        Game = mission.Game;
        Author = mission.Author;
        ReleaseYear = mission.ReleaseYear;
        Status = mission.Status;
        Rating = mission.Rating;
        Tags = mission.Tags;
        Notes = mission.Notes;
        DateStarted = mission.DateStarted;
        DateCompleted = mission.DateCompleted;
        FolderPath = mission.FolderPath;
    }

    private async Task SaveAsync()
    {
        var mission = new FanMission
        {
            Id = _id,
            Title = Title,
            Game = Game,
            Author = Author,
            ReleaseYear = ReleaseYear,
            Status = Status,
            Rating = Rating,
            Tags = Tags,
            Notes = Notes,
            DateStarted = DateStarted,
            DateCompleted = DateCompleted,
            FolderPath = FolderPath
        };

        if (_id == 0)
            await _missionRepository.AddAsync(mission);
        else
            await _missionRepository.UpdateAsync(mission);

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter MissionEditViewModelTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/ViewModels/MissionEditViewModel.cs tests/ThiefManager.Tests/ViewModels/MissionEditViewModelTests.cs
git commit -m "Add MissionEditViewModel for adding and editing missions"
```

---

### Task 10: SettingsViewModel

**Files:**
- Create: `src/ThiefManager/ViewModels/SettingsViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/SettingsViewModelTests.cs`
- Test helper: `tests/ThiefManager.Tests/Fakes/FakeSettingsRepository.cs`

**Interfaces:**
- Consumes: `ISettingsRepository` (Task 7), `AppSettings` (Task 2).
- Produces: `class SettingsViewModel(ISettingsRepository settingsRepository)` : `ObservableObject` with `Thief1FmFolder`, `Thief2FmFolder`, `Thief1ExePath`, `Thief2ExePath` properties, `IAsyncRelayCommand LoadCommand`, `IAsyncRelayCommand SaveCommand`, `event EventHandler? Saved`. Consumed by `SettingsWindow.xaml` (Task 12) and by the composition root (Task 13) to feed `MainViewModel.ConfigureExePaths`.

- [ ] **Step 1: Add a fake settings repository test helper**

```csharp
// tests/ThiefManager.Tests/Fakes/FakeSettingsRepository.cs
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeSettingsRepository : ISettingsRepository
{
    private AppSettings _settings = new() { Id = 1 };

    public Task<AppSettings> GetAsync() => Task.FromResult(new AppSettings
    {
        Id = _settings.Id,
        Thief1FmFolder = _settings.Thief1FmFolder,
        Thief2FmFolder = _settings.Thief2FmFolder,
        Thief1ExePath = _settings.Thief1ExePath,
        Thief2ExePath = _settings.Thief2ExePath
    });

    public Task SaveAsync(AppSettings settings)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/ThiefManager.Tests/ViewModels/SettingsViewModelTests.cs
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public async Task LoadCommand_PopulatesPropertiesFromRepository()
    {
        var repo = new FakeSettingsRepository();
        await repo.SaveAsync(new ThiefManager.Models.AppSettings { Thief1ExePath = @"C:\Thief.exe" });
        var vm = new SettingsViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Thief.exe", vm.Thief1ExePath);
    }

    [Fact]
    public async Task SaveCommand_PersistsPropertiesAndRaisesSaved()
    {
        var repo = new FakeSettingsRepository();
        var vm = new SettingsViewModel(repo)
        {
            Thief1FmFolder = @"C:\fms1",
            Thief2FmFolder = @"C:\fms2",
            Thief1ExePath = @"C:\Thief.exe",
            Thief2ExePath = @"C:\Thief2.exe"
        };
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
        var saved = await repo.GetAsync();
        Assert.Equal(@"C:\fms1", saved.Thief1FmFolder);
        Assert.Equal(@"C:\Thief2.exe", saved.Thief2ExePath);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter SettingsViewModelTests`
Expected: FAIL (compile error — `SettingsViewModel` doesn't exist yet).

- [ ] **Step 4: Implement `SettingsViewModel`**

```csharp
// src/ThiefManager/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;

    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public event EventHandler? Saved;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty] private string? thief1FmFolder;
    [ObservableProperty] private string? thief2FmFolder;
    [ObservableProperty] private string? thief1ExePath;
    [ObservableProperty] private string? thief2ExePath;

    private async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        Thief1FmFolder = settings.Thief1FmFolder;
        Thief2FmFolder = settings.Thief2FmFolder;
        Thief1ExePath = settings.Thief1ExePath;
        Thief2ExePath = settings.Thief2ExePath;
    }

    private async Task SaveAsync()
    {
        await _settingsRepository.SaveAsync(new AppSettings
        {
            Thief1FmFolder = Thief1FmFolder,
            Thief2FmFolder = Thief2FmFolder,
            Thief1ExePath = Thief1ExePath,
            Thief2ExePath = Thief2ExePath
        });

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter SettingsViewModelTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/ViewModels/SettingsViewModel.cs tests/ThiefManager.Tests/ViewModels/SettingsViewModelTests.cs tests/ThiefManager.Tests/Fakes/FakeSettingsRepository.cs
git commit -m "Add SettingsViewModel for configuring FM folders and exe paths"
```

---

### Task 11: ScanViewModel

**Files:**
- Create: `src/ThiefManager/Services/IDirectoryReader.cs`
- Create: `src/ThiefManager/Services/DirectoryReader.cs`
- Create: `src/ThiefManager/ViewModels/ScanCandidateViewModel.cs`
- Create: `src/ThiefManager/ViewModels/ScanViewModel.cs`
- Test: `tests/ThiefManager.Tests/ViewModels/ScanViewModelTests.cs`

**Interfaces:**
- Consumes: `ScanService`, `ScanCandidate` (Task 3), `IMissionRepository` (Task 7), `FanMission`, `GameTitle` (Task 2).
- Produces: `interface IDirectoryReader { IReadOnlyList<string> GetSubdirectories(string path); }` and real `DirectoryReader`; `class ScanCandidateViewModel { string SuggestedTitle; string FolderPath; bool IsSelected; }`; `class ScanViewModel(IDirectoryReader directoryReader, IMissionRepository missionRepository)` with `IAsyncRelayCommand ScanCommand(GameTitle game, string fmFolder)`-equivalent (implemented as a method taking parameters, invoked by the view), `ObservableCollection<ScanCandidateViewModel> Candidates`, `IAsyncRelayCommand ImportSelectedCommand`. Consumed by `MainWindow.xaml`'s scan panel (Task 12).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/ThiefManager.Tests/ViewModels/ScanViewModelTests.cs
using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class FakeDirectoryReader : IDirectoryReader
{
    private readonly IReadOnlyList<string> _subdirectories;
    public FakeDirectoryReader(params string[] subdirectories) => _subdirectories = subdirectories;
    public IReadOnlyList<string> GetSubdirectories(string path) => _subdirectories;
}

public class ScanViewModelTests
{
    [Fact]
    public async Task Scan_PopulatesCandidatesExcludingAlreadyCatalogedFolders()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Existing", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Existing" });
        var directoryReader = new FakeDirectoryReader(@"C:\fms\Existing", @"C:\fms\NewOne");
        var vm = new ScanViewModel(directoryReader, repo);

        await vm.Scan(GameTitle.Thief1, @"C:\fms");

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", candidate.SuggestedTitle);
        Assert.Equal(@"C:\fms\NewOne", candidate.FolderPath);
    }

    [Fact]
    public async Task ImportSelectedCommand_AddsCheckedCandidatesToRepositoryAndClearsThem()
    {
        var repo = new FakeMissionRepository();
        var directoryReader = new FakeDirectoryReader(@"C:\fms\NewOne", @"C:\fms\NewTwo");
        var vm = new ScanViewModel(directoryReader, repo);
        await vm.Scan(GameTitle.Thief2, @"C:\fms");
        vm.Candidates.Single(c => c.SuggestedTitle == "NewOne").IsSelected = true;

        await vm.ImportSelectedCommand.ExecuteAsync(null);

        var imported = Assert.Single(repo.Missions);
        Assert.Equal("NewOne", imported.Title);
        Assert.Equal(GameTitle.Thief2, imported.Game);
        Assert.Single(vm.Candidates);
        Assert.Equal("NewTwo", vm.Candidates.Single().SuggestedTitle);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/ThiefManager.Tests --filter ScanViewModelTests`
Expected: FAIL (compile error — `IDirectoryReader`/`ScanViewModel` don't exist yet).

- [ ] **Step 3: Implement `IDirectoryReader`, `DirectoryReader`, `ScanCandidateViewModel`, and `ScanViewModel`**

```csharp
// src/ThiefManager/Services/IDirectoryReader.cs
namespace ThiefManager.Services;

public interface IDirectoryReader
{
    IReadOnlyList<string> GetSubdirectories(string path);
}
```

```csharp
// src/ThiefManager/Services/DirectoryReader.cs
namespace ThiefManager.Services;

public class DirectoryReader : IDirectoryReader
{
    public IReadOnlyList<string> GetSubdirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();
}
```

```csharp
// src/ThiefManager/ViewModels/ScanCandidateViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThiefManager.ViewModels;

public partial class ScanCandidateViewModel : ObservableObject
{
    public ScanCandidateViewModel(string suggestedTitle, string folderPath)
    {
        SuggestedTitle = suggestedTitle;
        FolderPath = folderPath;
    }

    [ObservableProperty] private string suggestedTitle;
    [ObservableProperty] private string folderPath;
    [ObservableProperty] private bool isSelected;
}
```

```csharp
// src/ThiefManager/ViewModels/ScanViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IMissionRepository _missionRepository;
    private GameTitle _lastScannedGame;

    public ScanViewModel(IDirectoryReader directoryReader, IMissionRepository missionRepository)
    {
        _directoryReader = directoryReader;
        _missionRepository = missionRepository;
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync);
    }

    public ObservableCollection<ScanCandidateViewModel> Candidates { get; } = new();
    public IAsyncRelayCommand ImportSelectedCommand { get; }

    public async Task Scan(GameTitle game, string fmFolder)
    {
        _lastScannedGame = game;
        var existing = await _missionRepository.GetAllAsync();
        var subfolders = _directoryReader.GetSubdirectories(fmFolder);
        var newCandidates = ScanService.FindNewCandidates(subfolders, existing.Select(m => m.FolderPath));

        Candidates.Clear();
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(candidate.SuggestedTitle, candidate.FolderPath));
    }

    private async Task ImportSelectedAsync()
    {
        var selected = Candidates.Where(c => c.IsSelected).ToList();
        foreach (var candidate in selected)
        {
            await _missionRepository.AddAsync(new FanMission
            {
                Title = candidate.SuggestedTitle,
                Game = _lastScannedGame,
                FolderPath = candidate.FolderPath
            });
            Candidates.Remove(candidate);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/ThiefManager.Tests --filter ScanViewModelTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/ThiefManager/Services/IDirectoryReader.cs src/ThiefManager/Services/DirectoryReader.cs src/ThiefManager/ViewModels/ScanCandidateViewModel.cs src/ThiefManager/ViewModels/ScanViewModel.cs tests/ThiefManager.Tests/ViewModels/ScanViewModelTests.cs
git commit -m "Add ScanViewModel for discovering and importing new fan missions"
```

---

### Task 12: XAML views

**Files:**
- Modify: `src/ThiefManager/MainWindow.xaml`, `src/ThiefManager/MainWindow.xaml.cs`
- Create: `src/ThiefManager/Views/SettingsWindow.xaml`, `src/ThiefManager/Views/SettingsWindow.xaml.cs`
- Create: `src/ThiefManager/Views/ScanWindow.xaml`, `src/ThiefManager/Views/ScanWindow.xaml.cs`
- Create: `src/ThiefManager/Converters/EnumToStringConverter.cs`

**Interfaces:**
- Consumes: `MainViewModel` (Task 8), `MissionEditViewModel` (Task 9), `SettingsViewModel` (Task 10), `ScanViewModel` (Task 11).
- Produces: a working UI. No new testable logic — this task is verified manually (Task 14) since WPF views are not practically unit-testable without a UI automation framework, which is out of scope for v1.

- [ ] **Step 1: Build `MainWindow.xaml`**

```xml
<!-- src/ThiefManager/MainWindow.xaml -->
<Window x:Class="ThiefManager.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Thief Fan Mission Manager" Height="600" Width="1000">
    <DockPanel Margin="8">
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File">
                <MenuItem Header="_Settings..." Click="OpenSettings_Click"/>
                <MenuItem Header="_Scan for New Missions..." Click="OpenScan_Click"/>
            </MenuItem>
        </Menu>

        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="0,8,0,8">
            <TextBlock Text="Game:" VerticalAlignment="Center" Margin="0,0,4,0"/>
            <ComboBox Width="100" SelectedItem="{Binding GameFilter}" Margin="0,0,12,0">
                <ComboBox.Items>
                    <x:Null/>
                    <local:GameTitle xmlns:local="clr-namespace:ThiefManager.Models">Thief1</local:GameTitle>
                    <local:GameTitle xmlns:local="clr-namespace:ThiefManager.Models">Thief2</local:GameTitle>
                </ComboBox.Items>
            </ComboBox>
            <TextBlock Text="Tag:" VerticalAlignment="Center" Margin="0,0,4,0"/>
            <TextBox Width="120" Text="{Binding TagFilter, UpdateSourceTrigger=PropertyChanged}" Margin="0,0,12,0"/>
            <Button Content="Add Mission" Click="AddMission_Click" Margin="0,0,8,0"/>
            <Button Content="Play" Command="{Binding LaunchSelectedCommand}" Margin="0,0,8,0"/>
            <Button Content="Delete" Command="{Binding DeleteSelectedCommand}"/>
        </StackPanel>

        <TextBlock DockPanel.Dock="Top" Text="{Binding LaunchError}" Foreground="Red" Margin="0,0,0,8"/>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="3*"/>
            </Grid.ColumnDefinitions>

            <ListView Grid.Column="0" ItemsSource="{Binding VisibleMissions}"
                      SelectedItem="{Binding SelectedMission}"
                      MouseDoubleClick="MissionList_DoubleClick">
                <ListView.View>
                    <GridView>
                        <GridViewColumn Header="Title" DisplayMemberBinding="{Binding Title}" Width="220"/>
                        <GridViewColumn Header="Game" DisplayMemberBinding="{Binding Game}" Width="80"/>
                        <GridViewColumn Header="Status" DisplayMemberBinding="{Binding Status}" Width="100"/>
                        <GridViewColumn Header="Rating" DisplayMemberBinding="{Binding Rating}" Width="60"/>
                    </GridView>
                </ListView.View>
            </ListView>

            <ContentControl Grid.Column="1" x:Name="EditPanelHost" Margin="8,0,0,0"/>
        </Grid>
    </DockPanel>
</Window>
```

Note: WPF `ComboBox` bound directly to an enum via `x:Null`/typed items is fiddly in pure XAML; simplify by exposing filter options as strings from the code-behind instead:

```xml
<ComboBox Width="100" Margin="0,0,12,0"
          ItemsSource="{Binding GameFilterOptions}"
          SelectedItem="{Binding GameFilterDisplay}"/>
```

Add to `MainViewModel` (edit the file created in Task 8) two small display-only members that wrap `GameFilter`:

```csharp
// Add inside MainViewModel class (src/ThiefManager/ViewModels/MainViewModel.cs)
public string[] GameFilterOptions { get; } = { "(All)", "Thief1", "Thief2" };

public string GameFilterDisplay
{
    get => GameFilter?.ToString() ?? "(All)";
    set => GameFilter = value == "(All)" ? null : Enum.Parse<GameTitle>(value);
}
```

Use `GameFilterDisplay` in the XAML `ComboBox` binding above instead of the raw enum approach.

- [ ] **Step 2: Wire `MainWindow.xaml.cs`**

```csharp
// src/ThiefManager/MainWindow.xaml.cs
using System.Windows;
using System.Windows.Input;
using ThiefManager.Data;
using ThiefManager.ViewModels;
using ThiefManager.Views;

namespace ThiefManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IMissionRepository _missionRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly Services.LaunchService _launchService;
    private readonly Services.IDirectoryReader _directoryReader;

    public MainWindow(
        MainViewModel viewModel,
        IMissionRepository missionRepository,
        ISettingsRepository settingsRepository,
        Services.LaunchService launchService,
        Services.IDirectoryReader directoryReader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _missionRepository = missionRepository;
        _settingsRepository = settingsRepository;
        _launchService = launchService;
        _directoryReader = directoryReader;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void AddMission_Click(object sender, RoutedEventArgs e)
    {
        var editViewModel = new MissionEditViewModel(_missionRepository);
        var editWindow = new MissionEditWindow(editViewModel) { Owner = this };
        editViewModel.Saved += async (_, _) =>
        {
            editWindow.Close();
            await _viewModel.LoadCommand.ExecuteAsync(null);
        };
        editWindow.ShowDialog();
    }

    private void MissionList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedMission is null)
            return;

        var editViewModel = new MissionEditViewModel(_missionRepository);
        editViewModel.LoadFrom(_viewModel.SelectedMission);
        var editWindow = new MissionEditWindow(editViewModel) { Owner = this };
        editViewModel.Saved += async (_, _) =>
        {
            editWindow.Close();
            await _viewModel.LoadCommand.ExecuteAsync(null);
        };
        editWindow.ShowDialog();
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsViewModel = new SettingsViewModel(_settingsRepository);
        await settingsViewModel.LoadCommand.ExecuteAsync(null);
        var settingsWindow = new SettingsWindow(settingsViewModel) { Owner = this };
        settingsViewModel.Saved += async (_, _) =>
        {
            var settings = await _settingsRepository.GetAsync();
            _viewModel.ConfigureExePaths(settings.Thief1ExePath, settings.Thief2ExePath);
            settingsWindow.Close();
        };
        settingsWindow.ShowDialog();
    }

    private async void OpenScan_Click(object sender, RoutedEventArgs e)
    {
        var settings = await _settingsRepository.GetAsync();
        var scanViewModel = new ScanViewModel(_directoryReader, _missionRepository);
        var scanWindow = new ScanWindow(scanViewModel, settings) { Owner = this };
        scanWindow.Closed += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
        scanWindow.ShowDialog();
    }
}
```

Note: this references `MissionEditWindow`, which is added alongside `SettingsWindow`/`ScanWindow` in this task (a fourth small view) — create `src/ThiefManager/Views/MissionEditWindow.xaml` and `.xaml.cs` following the same pattern as `SettingsWindow` below, with a form binding to each `MissionEditViewModel` property and a "Save" button bound to `SaveCommand`.

- [ ] **Step 3: Build `SettingsWindow.xaml` and code-behind**

```xml
<!-- src/ThiefManager/Views/SettingsWindow.xaml -->
<Window x:Class="ThiefManager.Views.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Settings" Height="260" Width="500">
    <StackPanel Margin="12">
        <TextBlock Text="Thief 1 FM Folder"/>
        <TextBox Text="{Binding Thief1FmFolder}" Margin="0,0,0,8"/>
        <TextBlock Text="Thief 1 Executable"/>
        <TextBox Text="{Binding Thief1ExePath}" Margin="0,0,0,8"/>
        <TextBlock Text="Thief 2 FM Folder"/>
        <TextBox Text="{Binding Thief2FmFolder}" Margin="0,0,0,8"/>
        <TextBlock Text="Thief 2 Executable"/>
        <TextBox Text="{Binding Thief2ExePath}" Margin="0,0,0,8"/>
        <Button Content="Save" Command="{Binding SaveCommand}" HorizontalAlignment="Right"/>
    </StackPanel>
</Window>
```

```csharp
// src/ThiefManager/Views/SettingsWindow.xaml.cs
using System.Windows;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 4: Build `ScanWindow.xaml` and code-behind**

```xml
<!-- src/ThiefManager/Views/ScanWindow.xaml -->
<Window x:Class="ThiefManager.Views.ScanWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Scan for New Missions" Height="400" Width="500">
    <DockPanel Margin="12">
        <Button DockPanel.Dock="Bottom" Content="Import Selected" Command="{Binding ImportSelectedCommand}" HorizontalAlignment="Right" Margin="0,8,0,0"/>
        <ListView ItemsSource="{Binding Candidates}">
            <ListView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal">
                        <CheckBox IsChecked="{Binding IsSelected}" Margin="0,0,8,0"/>
                        <TextBlock Text="{Binding SuggestedTitle}"/>
                    </StackPanel>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>
    </DockPanel>
</Window>
```

```csharp
// src/ThiefManager/Views/ScanWindow.xaml.cs
using System.Windows;
using ThiefManager.Models;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class ScanWindow : Window
{
    public ScanWindow(ScanViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(settings.Thief1FmFolder))
                await viewModel.Scan(GameTitle.Thief1, settings.Thief1FmFolder);
            if (!string.IsNullOrWhiteSpace(settings.Thief2FmFolder))
                await viewModel.Scan(GameTitle.Thief2, settings.Thief2FmFolder);
        };
    }
}
```

Note: calling `Scan` twice (once per game) overwrites `Candidates` rather than merging in this simple v1 wiring. For a first version this is acceptable — the user scans one game at a time by re-opening the window after switching which folder they care about, or the `ScanWindow` can offer two buttons ("Scan Thief 1" / "Scan Thief 2") instead of the automatic double-scan on load. Use the two-button variant if double-scanning proves confusing during manual testing (Task 14).

- [ ] **Step 5: Build to verify the UI compiles**

Run: `dotnet build`
Expected: Build succeeds with 0 errors. Fix any XAML binding/namespace errors surfaced by the compiler (e.g. missing `xmlns:local` for the `MissionEditWindow` reference — add `xmlns:local="clr-namespace:ThiefManager"` to `MainWindow.xaml` if the C# compiler flags an unresolved type, though referencing `MissionEditWindow` from code-behind as done here does not require it).

- [ ] **Step 6: Commit**

```bash
git add src/ThiefManager/MainWindow.xaml src/ThiefManager/MainWindow.xaml.cs src/ThiefManager/Views
git commit -m "Add XAML views for main catalog, settings, scanning, and mission editing"
```

---

### Task 13: Composition root (`App.xaml.cs`)

**Files:**
- Modify: `src/ThiefManager/App.xaml.cs`

**Interfaces:**
- Consumes: every class from Tasks 2-12.
- Produces: a running app that creates/opens its SQLite database in `%LOCALAPPDATA%\ThiefManager\thiefmanager.db`, wires all services/repositories/viewmodels manually (no DI container needed given the small object graph), and shows `MainWindow`.

- [ ] **Step 1: Implement the composition root**

```csharp
// src/ThiefManager/App.xaml.cs
using System.IO;
using System.Windows;
using ThiefManager.Data;
using ThiefManager.Services;
using ThiefManager.ViewModels;

namespace ThiefManager;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThiefManager");
        Directory.CreateDirectory(appDataFolder);
        var dbPath = Path.Combine(appDataFolder, "thiefmanager.db");

        ThiefManagerDbContext CreateContext() => new(dbPath);
        using (var db = CreateContext())
            db.Database.EnsureCreated();

        var missionRepository = new MissionRepository(CreateContext);
        var settingsRepository = new SettingsRepository(CreateContext);
        var launchService = new LaunchService(new ProcessLauncher(), new FileExistsChecker());
        var directoryReader = new DirectoryReader();

        var mainViewModel = new MainViewModel(missionRepository, launchService);
        var settings = await settingsRepository.GetAsync();
        mainViewModel.ConfigureExePaths(settings.Thief1ExePath, settings.Thief2ExePath);

        var mainWindow = new MainWindow(mainViewModel, missionRepository, settingsRepository, launchService, directoryReader);
        mainWindow.Show();
    }
}
```

- [ ] **Step 2: Remove the default `StartupUri` from `App.xaml` so `OnStartup` controls window creation**

```xml
<!-- src/ThiefManager/App.xaml -->
<Application x:Class="ThiefManager.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources/>
</Application>
```

- [ ] **Step 3: Build and run to verify the app starts**

Run: `dotnet build` then `dotnet run --project src/ThiefManager`
Expected: The main window opens with an empty mission list and no errors in the console.

- [ ] **Step 4: Commit**

```bash
git add src/ThiefManager/App.xaml src/ThiefManager/App.xaml.cs
git commit -m "Wire composition root: SQLite startup and manual DI"
```

---

### Task 14: Manual end-to-end verification

**Files:** none (manual verification only).

- [ ] **Step 1: Run the full automated test suite**

Run: `dotnet test`
Expected: All tests across `ThiefManager.Tests` pass.

- [ ] **Step 2: Manually verify the add/edit/scan/launch flows**

1. Run `dotnet run --project src/ThiefManager`.
2. Click **Add Mission**, fill in a title/game/folder path, save — confirm it appears in the list.
3. Double-click the mission, change its status/rating/notes, save — confirm the list reflects the change.
4. Open **File > Settings**, set `Thief1ExePath` to a harmless test executable (e.g. `C:\Windows\System32\notepad.exe`) and `Thief1FmFolder` to a real folder containing a few subfolders, save.
5. Select a Thief1 mission, click **Play** — confirm Notepad launches (proves the launch mechanism works end-to-end) and no `LaunchError` is shown.
6. Select a mission and click **Play** for a game with no exe path configured — confirm a `LaunchError` message appears instead of a crash.
7. Open **File > Scan for New Missions**, confirm subfolders not already in the catalog appear as candidates, check one, click **Import Selected**, confirm it's added to the main list with status `NotPlayed`.
8. Select a mission and click **Delete** — confirm it disappears from the list and does not reappear after restarting the app (proves persistence).

- [ ] **Step 3: Report results**

Note which of the 8 checks in Step 2 passed, and file any failures as follow-up fixes before considering v1 done.
