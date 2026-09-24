namespace ThiefManager.Services;

public record ThiefGuildLookupResult(string? Author, int? ReleaseYear, string Tags, string Url, ThiefGuildSeriesInfo? Series = null);
