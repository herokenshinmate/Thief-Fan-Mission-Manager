namespace ThiefManager.Services;

public record ThiefGuildLookupResult(
    string? Author,
    int? ReleaseYear,
    string Tags,
    string Url,
    ThiefGuildSeriesInfo? Series = null,
    double? Rating = null,
    int? RatingCount = null,
    int? CampaignMissionCount = null,
    string? Description = null,
    ThiefGuildLink? SequelOf = null,
    ThiefGuildLink? HasSequel = null);
