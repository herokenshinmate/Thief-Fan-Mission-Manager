using System.Net.Http;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;

namespace ThiefManager.Services;

public class ThiefGuildLookupService : IThiefGuildLookupService
{
    private const string BaseUrl = "https://www.thiefguild.com";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly IBrowsingContext Browser = AngleSharp.BrowsingContext.New(Configuration.Default);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ThiefManagerApp/1.0 (personal fan-mission catalog; +https://www.thiefguild.com)");
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    public async Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        try
        {
            var keyword = ThiefGuildTitleMatcher.ExtractSearchKeyword(title);
            var searchUrl = $"{BaseUrl}/fanmissions/?search={Uri.EscapeDataString(keyword)}";
            var document = await LoadDocumentAsync(searchUrl);
            if (document is null)
                return null;

            foreach (var card in document.QuerySelectorAll("div.panel"))
            {
                var titleLink = card.QuerySelector("h4 a[href^='/fanmissions/']");
                if (titleLink is null)
                    continue;

                var candidateTitle = titleLink.TextContent.Trim();
                if (!ThiefGuildTitleMatcher.IsMatch(candidateTitle, title))
                    continue;

                var href = titleLink.GetAttribute("href");
                if (href is null)
                    continue;

                return BuildResult(card, card.TextContent, BaseUrl + href);
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public async Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var document = await LoadDocumentAsync(url);
            if (document is null)
                return null;

            return BuildResult(document, document.Body?.TextContent ?? string.Empty, url);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static ThiefGuildLookupResult BuildResult(IParentNode scope, string scopeText, string url)
    {
        var author = scope.QuerySelector("a[href^='/user/']")?.TextContent.Trim();

        var genres = scope.QuerySelectorAll("a.label.label-default[href*='genre=']")
            .Select(g => g.TextContent.Trim())
            .Where(g => g.Length > 0)
            .Distinct()
            .ToList();
        var tags = string.Join(", ", genres);

        int? releaseYear = null;
        var yearMatch = Regex.Match(scopeText, @"\((\d{4})\)");
        if (!yearMatch.Success)
            yearMatch = Regex.Match(scopeText, @"\b((?:19|20)\d{2})\b");
        if (yearMatch.Success && int.TryParse(yearMatch.Groups[1].Value, out var year))
            releaseYear = year;

        return new ThiefGuildLookupResult(
            string.IsNullOrWhiteSpace(author) ? null : author,
            releaseYear,
            tags,
            url);
    }

    private async Task<IDocument?> LoadDocumentAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var html = await response.Content.ReadAsStringAsync();
        return await Browser.OpenAsync(req => req.Content(html));
    }
}
