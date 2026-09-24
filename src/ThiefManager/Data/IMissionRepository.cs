using ThiefManager.Models;

namespace ThiefManager.Data;

public interface IMissionRepository
{
    Task<List<FanMission>> GetAllAsync();
    Task AddAsync(FanMission mission);
    Task UpdateAsync(FanMission mission);
    Task DeleteAsync(int id);

    /// <summary>
    /// Records the result of a Thief Guild series lookup by writing only the series columns, so a
    /// caller holding an older copy of the mission can't revert other fields the user has since
    /// edited. The series is only assigned if the mission still has none; SeriesLookupChecked is
    /// always set. A missing mission is ignored.
    /// </summary>
    Task ApplySeriesLookupAsync(int missionId, int? seriesId, int? seriesPosition);
}
