namespace ThiefManager.Services;

public record ThiefGuildSeriesInfo(int ThiefGuildSeriesId, string Name, int Position, IReadOnlyList<ThiefGuildSeriesPartInfo>? Parts = null);
