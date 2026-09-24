using Microsoft.EntityFrameworkCore;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.Data;

public class IgnoredFmRepository : IIgnoredFmRepository
{
    private readonly Func<ThiefManagerDbContext> _contextFactory;

    public IgnoredFmRepository(Func<ThiefManagerDbContext> contextFactory) => _contextFactory = contextFactory;

    public async Task<List<IgnoredFm>> GetAllAsync()
    {
        using var db = _contextFactory();
        return await db.IgnoredFms.AsNoTracking().ToListAsync();
    }

    public async Task AddAsync(GameTitle game, string name)
    {
        using var db = _contextFactory();
        var existingForGame = await db.IgnoredFms.Where(i => i.Game == game).ToListAsync();
        if (existingForGame.Any(i => FmNameMatcher.AreSimilar(i.Name, name)))
            return;

        db.IgnoredFms.Add(new IgnoredFm { Game = game, Name = name, IgnoredAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        using var db = _contextFactory();
        var entity = await db.IgnoredFms.FindAsync(id);
        if (entity is null)
            return;

        db.IgnoredFms.Remove(entity);
        await db.SaveChangesAsync();
    }
}
