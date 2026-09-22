namespace ThiefManager.Services;

public interface IThiefGuildLookupService
{
    Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title);
    Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url);
}
