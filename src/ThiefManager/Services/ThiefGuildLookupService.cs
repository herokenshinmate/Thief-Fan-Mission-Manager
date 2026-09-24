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
            var (document, finalUrl) = await LoadDocumentAsync(searchUrl);
            if (document is null)
                return null;

            // Thief Guild redirects straight to the mission's detail page when the search has
            // exactly one match, rather than showing a results list of div.panel cards.
            if (!string.Equals(finalUrl, searchUrl, StringComparison.Ordinal) && IsMissionDetailUrl(finalUrl))
            {
                var directTitle = ThiefGuildPageParser.ExtractDetailPageTitle(document);
                return ThiefGuildTitleMatcher.IsMatch(directTitle, title)
                    ? ThiefGuildPageParser.BuildResult(document, document.Body?.TextContent ?? string.Empty, finalUrl)
                    : null;
            }

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

                // Result cards lack the series block, so prefer the detail page; fall back to the
                // card if that second request fails.
                var detailUrl = BaseUrl + href;
                return await TryFetchDetailPageAsync(detailUrl)
                    ?? ThiefGuildPageParser.BuildResult(card, card.TextContent, detailUrl);
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

        return await TryFetchDetailPageAsync(url);
    }

    private static bool IsMissionDetailUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && Regex.IsMatch(uri.AbsolutePath, @"^/fanmissions/\d+/");

    private async Task<(IDocument? Document, string FinalUrl)> LoadDocumentAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return (null, url);

        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;
        var html = await response.Content.ReadAsStringAsync();
        var document = await Browser.OpenAsync(req => req.Content(html));
        return (document, finalUrl);
    }

    private async Task<ThiefGuildLookupResult?> TryFetchDetailPageAsync(string url)
    {
        try
        {
            var (document, finalUrl) = await LoadDocumentAsync(url);
            if (document is null)
                return null;

            return ThiefGuildPageParser.BuildResult(document, document.Body?.TextContent ?? string.Empty, finalUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
