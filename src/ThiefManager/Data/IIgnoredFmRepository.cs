using ThiefManager.Models;

namespace ThiefManager.Data;

public interface IIgnoredFmRepository
{
    Task<List<IgnoredFm>> GetAllAsync();
    Task AddAsync(GameTitle game, string name);
    Task DeleteAsync(int id);
}
