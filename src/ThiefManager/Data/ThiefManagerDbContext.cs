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
    public DbSet<Series> Series => Set<Series>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Series>().HasIndex(s => s.ThiefGuildSeriesId).IsUnique();
}
