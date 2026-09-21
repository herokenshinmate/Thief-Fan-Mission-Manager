using ThiefManager.Models;

namespace ThiefManager.Data;

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}
