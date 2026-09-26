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

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsManualNewDarkVersions()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.Thief1NewDarkVersion = "1.27";
        settings.Thief2NewDarkVersion = "1.26";

        await repo.SaveAsync(settings);
        var reloaded = await repo.GetAsync();

        Assert.Equal("1.27", reloaded.Thief1NewDarkVersion);
        Assert.Equal("1.26", reloaded.Thief2NewDarkVersion);
    }

    [Fact]
    public async Task GetAsync_WithNoSavedSettings_DefaultsShowMissionBriefingToTrue()
    {
        var repo = new SettingsRepository(CreateContext);

        var settings = await repo.GetAsync();

        Assert.True(settings.ShowMissionBriefing);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsShowMissionBriefing()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.ShowMissionBriefing = false;

        await repo.SaveAsync(settings);
        var reloaded = await repo.GetAsync();

        Assert.False(reloaded.ShowMissionBriefing);
    }

    [Fact]
    public async Task GetAsync_WithNoSavedSettings_DefaultsDoubleClickLaunchesPlayToTrue()
    {
        var repo = new SettingsRepository(CreateContext);

        var settings = await repo.GetAsync();

        Assert.True(settings.DoubleClickLaunchesPlay);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsDoubleClickLaunchesPlay()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.DoubleClickLaunchesPlay = false;

        await repo.SaveAsync(settings);
        var reloaded = await repo.GetAsync();

        Assert.False(reloaded.DoubleClickLaunchesPlay);
    }

    [Fact]
    public async Task GetAsync_WithNoSavedSettings_DefaultsWarnOnNewDarkVersionMismatchToTrue()
    {
        var repo = new SettingsRepository(CreateContext);

        var settings = await repo.GetAsync();

        Assert.True(settings.WarnOnNewDarkVersionMismatch);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsWarnOnNewDarkVersionMismatch()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.WarnOnNewDarkVersionMismatch = false;

        await repo.SaveAsync(settings);
        var reloaded = await repo.GetAsync();

        Assert.False(reloaded.WarnOnNewDarkVersionMismatch);
    }

    [Fact]
    public async Task SaveAsync_TwiceInARow_PersistsDownloadsFoldersOnTheUpdatePath()
    {
        var repo = new SettingsRepository(CreateContext);
        var settings = await repo.GetAsync();
        settings.Thief1DownloadsFolder = @"C:\Downloads\Thief1";
        await repo.SaveAsync(settings);

        settings = await repo.GetAsync();
        settings.Thief2DownloadsFolder = @"C:\Downloads\Thief2";
        await repo.SaveAsync(settings);

        var reloaded = await repo.GetAsync();

        Assert.Equal(@"C:\Downloads\Thief1", reloaded.Thief1DownloadsFolder);
        Assert.Equal(@"C:\Downloads\Thief2", reloaded.Thief2DownloadsFolder);
    }

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

    public void Dispose()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(50);

            if (File.Exists(_dbPath))
                File.Delete(_dbPath);

            // Also try to delete the WAL and SHM files that SQLite creates
            var walPath = _dbPath + "-wal";
            var shmPath = _dbPath + "-shm";
            if (File.Exists(walPath))
                File.Delete(walPath);
            if (File.Exists(shmPath))
                File.Delete(shmPath);
        }
        catch
        {
            // Ignore errors during cleanup - SQLite may still hold locks
        }
    }
}
