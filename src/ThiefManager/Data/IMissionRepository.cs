using ThiefManager.Models;

namespace ThiefManager.Data;

public interface IMissionRepository
{
    Task<List<FanMission>> GetAllAsync();
    Task AddAsync(FanMission mission);
    Task UpdateAsync(FanMission mission);
    Task DeleteAsync(int id);
}
