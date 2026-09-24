using ThiefManager.Data;
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Data;

public class SeriesRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"thiefmanager-test-{Guid.NewGuid()}.db");
    private ThiefManagerDbContext CreateContext() => new(_dbPath);

    public SeriesRepositoryTests()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetOrCreateByThiefGuildIdAsync_CreatesThenReusesAndRenames()
    {
        var repo = new SeriesRepository(CreateContext);

        var first = await repo.GetOrCreateByThiefGuildIdAsync(66445, "Book of Prophecy");
        var second = await repo.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");

        Assert.Equal(first.Id, second.Id);
        var stored = Assert.Single(await repo.GetAllAsync());
        Assert.Equal("The Book of Prophecy", stored.Name);
        Assert.Equal(66445, stored.ThiefGuildSeriesId);
        Assert.True(stored.IsExpanded);
    }

    [Fact]
    public async Task GetOrCreateByThiefGuildIdAsync_AdoptsSameNamedManualSeries()
    {
        var repo = new SeriesRepository(CreateContext);
        var manual = await repo.GetOrCreateByNameAsync("the book of prophecy");

        var fromThiefGuild = await repo.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");

        Assert.Equal(manual.Id, fromThiefGuild.Id);
        var stored = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(66445, stored.ThiefGuildSeriesId);
    }

    [Fact]
    public async Task GetOrCreateByNameAsync_MatchesCaseInsensitivelyAndTrims()
    {
        var repo = new SeriesRepository(CreateContext);

        var first = await repo.GetOrCreateByNameAsync("  Keeper Chronicles ");
        var second = await repo.GetOrCreateByNameAsync("keeper chronicles");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Keeper Chronicles", Assert.Single(await repo.GetAllAsync()).Name);
    }

    [Fact]
    public async Task RenameAsync_And_SetExpandedAsync_Persist()
    {
        var repo = new SeriesRepository(CreateContext);
        var series = await repo.GetOrCreateByNameAsync("Old");

        await repo.RenameAsync(series.Id, " New ");
        await repo.SetExpandedAsync(series.Id, false);

        var stored = Assert.Single(await repo.GetAllAsync());
        Assert.Equal("New", stored.Name);
        Assert.False(stored.IsExpanded);
    }

    [Fact]
    public async Task DeleteAsync_DetachesMembersAndMarksThemChecked()
    {
        var repo = new SeriesRepository(CreateContext);
        var missions = new MissionRepository(CreateContext);
        var series = await repo.GetOrCreateByNameAsync("S");
        await missions.AddAsync(new FanMission { Title = "M", FolderPath = "m", SeriesId = series.Id, SeriesPosition = 2 });

        await repo.DeleteAsync(series.Id);

        Assert.Empty(await repo.GetAllAsync());
        var mission = Assert.Single(await missions.GetAllAsync());
        Assert.Null(mission.SeriesId);
        Assert.Null(mission.SeriesPosition);
        Assert.True(mission.SeriesLookupChecked);
    }

    [Fact]
    public async Task DeleteOrphansAsync_RemovesOnlyUnreferencedSeries()
    {
        var repo = new SeriesRepository(CreateContext);
        var missions = new MissionRepository(CreateContext);
        var used = await repo.GetOrCreateByNameAsync("Used");
        await repo.GetOrCreateByNameAsync("Orphan");
        await missions.AddAsync(new FanMission { Title = "M", FolderPath = "m", SeriesId = used.Id });

        await repo.DeleteOrphansAsync();

        Assert.Equal("Used", Assert.Single(await repo.GetAllAsync()).Name);
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
