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
