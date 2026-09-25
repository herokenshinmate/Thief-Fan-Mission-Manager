using ThiefManager.Models;

namespace ThiefManager.Data;

public interface IMissionRepository
{
    Task<List<FanMission>> GetAllAsync();
    Task AddAsync(FanMission mission);
    Task UpdateAsync(FanMission mission);
    Task DeleteAsync(int id);

    /// <summary>
    /// Records a Thief Guild fetch by writing only the columns a fetch owns, so a caller holding an
    /// older copy of the mission can't revert fields the user has since edited: the Thief Guild
    /// fields and ThiefGuildMetadataVersion are copied from <paramref name="fetched"/>; Author,
    /// ReleaseYear and Tags only fill blanks; the series is only assigned when the stored mission
    /// has no series and SeriesLookupChecked is false; SeriesLookupChecked is OR-ed in. A missing
    /// mission is ignored.
    /// </summary>
    Task ApplyThiefGuildMetadataAsync(FanMission fetched);
}
