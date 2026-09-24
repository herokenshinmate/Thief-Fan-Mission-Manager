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

    [Fact]
    public async Task ApplySeriesLookupAsync_WritesOnlySeriesColumns()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief2, FolderPath = "m" });
        var staleCopy = (await repo.GetAllAsync()).Single();
        var userCopy = (await repo.GetAllAsync()).Single();
        userCopy.Status = MissionStatus.Completed;
        userCopy.Rating = 4;
        await repo.UpdateAsync(userCopy);

        await repo.ApplySeriesLookupAsync(staleCopy.Id, 7, 3);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(MissionStatus.Completed, reloaded.Status);
        Assert.Equal(4, reloaded.Rating);
        Assert.Equal(7, reloaded.SeriesId);
        Assert.Equal(3, reloaded.SeriesPosition);
        Assert.True(reloaded.SeriesLookupChecked);
    }

    [Fact]
    public async Task ApplySeriesLookupAsync_DoesNotReplaceExistingSeries()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", FolderPath = "m", SeriesId = 1, SeriesPosition = 9 });
        var id = (await repo.GetAllAsync()).Single().Id;

        await repo.ApplySeriesLookupAsync(id, 2, 3);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(1, reloaded.SeriesId);
        Assert.Equal(9, reloaded.SeriesPosition);
        Assert.True(reloaded.SeriesLookupChecked);
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
