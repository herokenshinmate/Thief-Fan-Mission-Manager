using ThiefManager.Models;

namespace ThiefManager.Data;

public interface ISeriesRepository
{
    Task<List<Series>> GetAllAsync();

    /// <summary>
    /// Returns the series with this Thief Guild id (renaming it if Thief Guild's name changed).
    /// Failing that, adopts a manually-created series with the same name and no Thief Guild id,
    /// so a series typed by hand and later found on Thief Guild stays one group. Otherwise creates it.
    /// </summary>
    Task<Series> GetOrCreateByThiefGuildIdAsync(int thiefGuildSeriesId, string name);

    /// <summary>Case-insensitive match on the trimmed name, creating the series if none matches.</summary>
    Task<Series> GetOrCreateByNameAsync(string name);

    Task RenameAsync(int id, string name);
    Task SetExpandedAsync(int id, bool isExpanded);

    /// <summary>
    /// Deletes the series and detaches its members. Members are marked series-checked so the
    /// startup backfill doesn't regroup them.
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>Deletes every series that no mission references.</summary>
    Task DeleteOrphansAsync();
}
