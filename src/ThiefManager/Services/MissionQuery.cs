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
        InstallStatus? installStatusFilter = null,
        string? authorFilter = null)
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

        if (!string.IsNullOrWhiteSpace(authorFilter))
        {
            query = query.Where(m => (m.Author ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(author => author.Contains(authorFilter, StringComparison.OrdinalIgnoreCase)));
        }

        object KeySelector(FanMission m) => sortField switch
        {
            SortField.Title => m.Title,
            SortField.Status => m.Status,
            SortField.InstallStatus => m.InstallStatus,
            SortField.Rating => m.Rating ?? -1,
            SortField.Author => m.Author ?? string.Empty,
            SortField.Tags => m.Tags,
            SortField.MissionType => m.CampaignMissionCount ?? 1,
            _ => m.Title
        };

        if (sortField == SortField.ThiefGuildRating)
        {
            // Unrated missions (no Thief Guild link, or no ratings yet) sink to the bottom whichever
            // direction is chosen, so sorting descending shows the best-rated first.
            var byPresence = query.OrderBy(m => m.ThiefGuildRating is null);
            query = ascending ? byPresence.ThenBy(m => m.ThiefGuildRating) : byPresence.ThenByDescending(m => m.ThiefGuildRating);
        }
        else
        {
            query = ascending ? query.OrderBy(KeySelector) : query.OrderByDescending(KeySelector);
        }

        return query.ToList();
    }
}
