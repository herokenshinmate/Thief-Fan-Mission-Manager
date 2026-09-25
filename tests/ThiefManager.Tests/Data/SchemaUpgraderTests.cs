using Microsoft.Data.Sqlite;
using ThiefManager.Data;
using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Data;

public class SchemaUpgraderTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"thiefmanager-test-{Guid.NewGuid()}.db");
    private ThiefManagerDbContext CreateContext() => new(_dbPath);

    private void CreatePreSeriesDatabase()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE "Series";
            ALTER TABLE "FanMissions" DROP COLUMN "SeriesId";
            ALTER TABLE "FanMissions" DROP COLUMN "SeriesPosition";
            ALTER TABLE "FanMissions" DROP COLUMN "SeriesLookupChecked";
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task EnsureColumns_OnPreSeriesDatabase_AddsSeriesSchema()
    {
        CreatePreSeriesDatabase();

        SchemaUpgrader.EnsureColumns(_dbPath);

        var seriesRepo = new SeriesRepository(CreateContext);
        var missionRepo = new MissionRepository(CreateContext);
        var series = await seriesRepo.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");
        await missionRepo.AddAsync(new FanMission { Title = "P3", FolderPath = "p3", SeriesId = series.Id, SeriesPosition = 3 });
        var mission = Assert.Single(await missionRepo.GetAllAsync());
        Assert.Equal(series.Id, mission.SeriesId);
        Assert.Equal(3, mission.SeriesPosition);
        Assert.False(mission.SeriesLookupChecked);
        Assert.True(Assert.Single(await seriesRepo.GetAllAsync()).IsExpanded);
    }

    [Fact]
    public async Task EnsureColumns_RunTwice_IsIdempotent()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();

        SchemaUpgrader.EnsureColumns(_dbPath);
        SchemaUpgrader.EnsureColumns(_dbPath);

        Assert.Empty(await new SeriesRepository(CreateContext).GetAllAsync());
    }

    [Fact]
    public async Task EnsureColumns_OnPreMetadataDatabase_AddsMetadataSchema()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TABLE "SeriesParts";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildRating";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildRatingCount";
                ALTER TABLE "FanMissions" DROP COLUMN "CampaignMissionCount";
                ALTER TABLE "FanMissions" DROP COLUMN "Description";
                ALTER TABLE "FanMissions" DROP COLUMN "SequelOfTitle";
                ALTER TABLE "FanMissions" DROP COLUMN "SequelOfUrl";
                ALTER TABLE "FanMissions" DROP COLUMN "HasSequelTitle";
                ALTER TABLE "FanMissions" DROP COLUMN "HasSequelUrl";
                ALTER TABLE "FanMissions" DROP COLUMN "ThiefGuildMetadataVersion";
                """;
            command.ExecuteNonQuery();
        }

        SchemaUpgrader.EnsureColumns(_dbPath);

        var missionRepo = new MissionRepository(CreateContext);
        var seriesRepo = new SeriesRepository(CreateContext);
        await missionRepo.AddAsync(new FanMission { Title = "M", FolderPath = "m", ThiefGuildRating = 9.5, Description = "d" });
        var series = await seriesRepo.GetOrCreateByNameAsync("S");
        await seriesRepo.ReplacePartsAsync(series.Id, new[] { new SeriesPart { Position = 1, Title = "P1" } });
        var mission = Assert.Single(await missionRepo.GetAllAsync());
        Assert.Equal(9.5, mission.ThiefGuildRating);
        Assert.Equal(0, mission.ThiefGuildMetadataVersion);
        Assert.Equal("P1", Assert.Single(await seriesRepo.GetAllPartsAsync()).Title);
    }

    [Fact]
    public async Task EnsureColumns_OnPreGameGroupsDatabase_AddsCollapsedColumns()
    {
        using (var db = CreateContext())
            db.Database.EnsureCreated();
        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                ALTER TABLE "Settings" DROP COLUMN "Thief1Collapsed";
                ALTER TABLE "Settings" DROP COLUMN "Thief2Collapsed";
                """;
            command.ExecuteNonQuery();
        }

        SchemaUpgrader.EnsureColumns(_dbPath);

        var repo = new SettingsRepository(CreateContext);
        await repo.SetGameCollapsedAsync(GameTitle.Thief1, true);
        Assert.True((await repo.GetAsync()).Thief1Collapsed);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(50);

            if (File.Exists(_dbPath))
                File.Delete(_dbPath);

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
