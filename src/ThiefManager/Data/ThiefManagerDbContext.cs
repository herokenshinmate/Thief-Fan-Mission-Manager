using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;

namespace ThiefManager.Data;

public class ThiefManagerDbContext : DbContext
{
    private readonly string _dbPath;

    public ThiefManagerDbContext(string dbPath) => _dbPath = dbPath;

    public DbSet<FanMission> FanMissions => Set<FanMission>();
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<IgnoredFm> IgnoredFms => Set<IgnoredFm>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");
}
