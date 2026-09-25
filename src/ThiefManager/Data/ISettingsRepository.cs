using ThiefManager.Models;

namespace ThiefManager.Data;

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);

    /// <summary>
    /// Remembers whether a game's banner in the mission list is collapsed. Writes only that
    /// column, so it never disturbs the folder settings (and SaveAsync never disturbs it).
    /// </summary>
    Task SetGameCollapsedAsync(GameTitle game, bool collapsed);
}
