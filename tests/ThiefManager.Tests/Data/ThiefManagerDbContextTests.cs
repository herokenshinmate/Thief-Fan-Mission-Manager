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
        try
        {
            // Try to delete the file, but don't fail if it's still in use
            if (File.Exists(_dbPath))
            {
                // Give SQLite a moment to release the lock
                System.Threading.Thread.Sleep(100);
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore if we can't delete it (file might still be locked)
        }
    }
}
