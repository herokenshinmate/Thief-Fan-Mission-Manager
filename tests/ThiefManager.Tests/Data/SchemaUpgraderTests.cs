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
