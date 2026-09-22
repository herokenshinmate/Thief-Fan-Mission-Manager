using ThiefManager.Models;

namespace ThiefManager.Services;

public static class MissionQuery
{
    public static IEnumerable<FanMission> Apply(
        IEnumerable<FanMission> missions,
        GameTitle? gameFilter,
        MissionStatus? statusFilter,
        string? tagFilter,
        SortField sortField,
        bool ascending,
        InstallStatus? installStatusFilter = null)
    {
        var query = missions.AsEnumerable();

        if (gameFilter.HasValue)
            query = query.Where(m => m.Game == gameFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(m => m.Status == statusFilter.Value);

        if (installStatusFilter.HasValue)
            query = query.Where(m => m.InstallStatus == installStatusFilter.Value);

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            query = query.Where(m => m.Tags
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(tag => tag.Equals(tagFilter, StringComparison.OrdinalIgnoreCase)));
        }

        object KeySelector(FanMission m) => sortField switch
        {
            SortField.Title => m.Title,
            SortField.Game => m.Game,
            SortField.Status => m.Status,
            SortField.Rating => m.Rating ?? -1,
            _ => m.Title
        };

        query = ascending ? query.OrderBy(KeySelector) : query.OrderByDescending(KeySelector);

        return query.ToList();
    }
}
