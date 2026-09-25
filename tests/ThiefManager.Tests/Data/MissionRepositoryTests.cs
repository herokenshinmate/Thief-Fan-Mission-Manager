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
    public async Task ApplyThiefGuildMetadataAsync_WritesOnlyFetchOwnedColumns()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief2, FolderPath = "m" });
        var fetched = (await repo.GetAllAsync()).Single();
        var userCopy = (await repo.GetAllAsync()).Single();
        userCopy.Status = MissionStatus.Completed;
        userCopy.Rating = 4;
        await repo.UpdateAsync(userCopy);

        fetched.ThiefGuildRating = 9.02;
        fetched.ThiefGuildRatingCount = 229;
        fetched.CampaignMissionCount = 1;
        fetched.Description = "A rainy night.";
        fetched.SequelOfTitle = "Between These Dark Walls";
        fetched.SequelOfUrl = "https://www.thiefguild.com/works/a";
        fetched.HasSequelTitle = "The Chalice of Souls";
        fetched.HasSequelUrl = "https://www.thiefguild.com/works/b";
        fetched.ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion;
        fetched.SeriesId = 7;
        fetched.SeriesPosition = 3;
        fetched.SeriesLookupChecked = true;
        await repo.ApplyThiefGuildMetadataAsync(fetched);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal(MissionStatus.Completed, reloaded.Status);
        Assert.Equal(4, reloaded.Rating);
        Assert.Equal(9.02, reloaded.ThiefGuildRating);
        Assert.Equal(229, reloaded.ThiefGuildRatingCount);
        Assert.Equal(1, reloaded.CampaignMissionCount);
        Assert.Equal("A rainy night.", reloaded.Description);
        Assert.Equal("Between These Dark Walls", reloaded.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", reloaded.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", reloaded.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", reloaded.HasSequelUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, reloaded.ThiefGuildMetadataVersion);
        Assert.Equal(7, reloaded.SeriesId);
        Assert.Equal(3, reloaded.SeriesPosition);
        Assert.True(reloaded.SeriesLookupChecked);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_FillsOnlyBlankAuthorYearAndTags()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "M", FolderPath = "m", Author = "Mine", Tags = "" });
        var fetched = (await repo.GetAllAsync()).Single();
        fetched.Author = "Theirs";
        fetched.ReleaseYear = 2014;
        fetched.Tags = "City";

        await repo.ApplyThiefGuildMetadataAsync(fetched);

        var reloaded = (await repo.GetAllAsync()).Single();
        Assert.Equal("Mine", reloaded.Author);
        Assert.Equal(2014, reloaded.ReleaseYear);
        Assert.Equal("City", reloaded.Tags);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_DoesNotAssignSeriesToCheckedOrGroupedMission()
    {
        var repo = new MissionRepository(CreateContext);
        await repo.AddAsync(new FanMission { Title = "Grouped", FolderPath = "g", SeriesId = 1, SeriesPosition = 9 });
        await repo.AddAsync(new FanMission { Title = "Ungrouped", FolderPath = "u", SeriesLookupChecked = true });
        foreach (var fetched in await repo.GetAllAsync())
        {
            fetched.SeriesId = 2;
            fetched.SeriesPosition = 3;
            await repo.ApplyThiefGuildMetadataAsync(fetched);
        }

        var all = await repo.GetAllAsync();
        Assert.Equal(1, all.Single(m => m.Title == "Grouped").SeriesId);
        Assert.Equal(9, all.Single(m => m.Title == "Grouped").SeriesPosition);
        Assert.Null(all.Single(m => m.Title == "Ungrouped").SeriesId);
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
