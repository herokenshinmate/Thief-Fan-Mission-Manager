using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace ThiefManager.Services;

public static class ThiefGuildPageParser
{
    public static ThiefGuildLookupResult BuildResult(IParentNode scope, string scopeText, string url)
    {
        var author = ExtractAuthor(scope);

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

        return new ThiefGuildLookupResult(author, releaseYear, tags, url, ExtractSeries(scope));
    }

    /// <summary>
    /// The mission detail page lists the credited author(s) under an "Author"/"Authors" heading,
    /// each linking to /user/&lt;id&gt;/&lt;name&gt; followed by a "Missions" button linking to
    /// /fanmissions?author=... . Earlier /user/ links on that same page (screenshot and video
    /// uploader credits) are unrelated, so the heading must be located first rather than picking
    /// the first /user/ link in document order, and every /user/ link under that heading is
    /// collected so co-authored missions credit all of them instead of just the first. Search
    /// result cards instead link each author from "by &lt;a href="?author=..."&gt;", or list plain
    /// text ("N authors") for missions with several credited authors, in which case no author can
    /// be determined.
    /// </summary>
    private static string? ExtractAuthor(IParentNode scope)
    {
        var authorHeading = scope.QuerySelectorAll("h4")
            .FirstOrDefault(h => h.TextContent.Trim().StartsWith("Author", StringComparison.OrdinalIgnoreCase));

        var authorLinks = authorHeading?.ParentElement?.QuerySelectorAll("a[href^='/user/']")
            ?? scope.QuerySelectorAll("a[href*='?author=']");

        var names = authorLinks
            .Select(a => a.TextContent.Trim())
            .Where(name => name.Length > 0)
            .Distinct()
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    /// <summary>
    /// Thief Guild redirects a single-match search straight to the mission's detail page instead of
    /// showing a results list. That page's &lt;title&gt; is "{Mission Title} - Fan Mission for ... - Thief Guild...".
    /// </summary>
    public static string ExtractDetailPageTitle(IDocument document) =>
        document.Title?.Split(" - ", 2).FirstOrDefault()?.Trim() ?? string.Empty;

    /// <summary>
    /// A mission detail page's header &lt;h6&gt; links the series ("/fanmissions?series=66445", text
    /// "The Book of Prophecy:") and then lists every member in series order: the other missions as
    /// links, and the current mission as plain, unlinked text. The current mission's 1-based index in
    /// that list is its position. Search result cards have no such block. Anything not matching
    /// this shape (no series link, no or several unlinked entries) yields null rather than a guess.
    /// </summary>
    public static ThiefGuildSeriesInfo? ExtractSeries(IParentNode scope)
    {
        var seriesLink = scope.QuerySelector("h6 a[href*='series=']");
        if (seriesLink?.ParentElement is not { } container)
            return null;

        var idMatch = Regex.Match(seriesLink.GetAttribute("href") ?? string.Empty, @"[?&]series=(\d+)");
        if (!idMatch.Success || !int.TryParse(idMatch.Groups[1].Value, out var seriesId))
            return null;

        var name = seriesLink.TextContent.Trim().TrimEnd(':').TrimEnd();
        if (name.Length == 0)
            return null;

        var memberCount = 0;
        int? currentPosition = null;
        var pastSeriesLink = false;
        foreach (var node in container.ChildNodes)
        {
            if (!pastSeriesLink)
            {
                pastSeriesLink = node == seriesLink;
                continue;
            }

            if (node is IElement element)
            {
                if (!element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase))
                    continue;

                var href = element.GetAttribute("href") ?? string.Empty;
                if (href.Contains("series="))
                    break; // start of another series block
                if (href.StartsWith("/fanmissions/", StringComparison.OrdinalIgnoreCase))
                    memberCount++;
            }
            else if (node.NodeType == NodeType.Text && !string.IsNullOrWhiteSpace(node.TextContent))
            {
                memberCount++;
                if (currentPosition is not null)
                    return null;
                currentPosition = memberCount;
            }
        }

        return currentPosition is int position ? new ThiefGuildSeriesInfo(seriesId, name, position) : null;
    }
}
