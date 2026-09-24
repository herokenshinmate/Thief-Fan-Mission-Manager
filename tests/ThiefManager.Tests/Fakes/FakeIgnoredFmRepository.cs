using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.Tests.Fakes;

public class FakeIgnoredFmRepository : IIgnoredFmRepository
{
    public List<IgnoredFm> IgnoredFms { get; } = new();

    public Task<List<IgnoredFm>> GetAllAsync() => Task.FromResult(IgnoredFms.ToList());

    public Task AddAsync(GameTitle game, string name)
    {
        if (IgnoredFms.Any(i => i.Game == game && FmNameMatcher.AreSimilar(i.Name, name)))
            return Task.CompletedTask;

        IgnoredFms.Add(new IgnoredFm
        {
            Id = IgnoredFms.Count == 0 ? 1 : IgnoredFms.Max(i => i.Id) + 1,
            Game = game,
            Name = name,
            IgnoredAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        IgnoredFms.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }
}
