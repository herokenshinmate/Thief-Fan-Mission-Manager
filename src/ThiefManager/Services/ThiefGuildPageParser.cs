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

        return new ThiefGuildLookupResult(author, releaseYear, tags, url);
    }

    /// <summary>
    /// The mission detail page lists the credited author under an "Author" heading, linking to
    /// /user/&lt;id&gt;/&lt;name&gt;. Earlier /user/ links on that same page (screenshot and video
    /// uploader credits) are unrelated, so the heading must be located first rather than picking
    /// the first /user/ link in document order. Search result cards instead link the author from
    /// "by &lt;a href="?author=..."&gt;", or list plain text ("N authors") for missions with several
    /// credited authors, in which case no author can be determined.
    /// </summary>
    private static string? ExtractAuthor(IParentNode scope)
    {
        var authorHeading = scope.QuerySelectorAll("h4")
            .FirstOrDefault(h => h.TextContent.Trim().Equals("Author", StringComparison.OrdinalIgnoreCase));
        var authorLink = authorHeading?.ParentElement?.QuerySelector("a[href^='/user/']")
            ?? scope.QuerySelector("a[href*='?author=']");

        var author = authorLink?.TextContent.Trim();
        return string.IsNullOrWhiteSpace(author) ? null : author;
    }

    /// <summary>
    /// Thief Guild redirects a single-match search straight to the mission's detail page instead of
    /// showing a results list. That page's &lt;title&gt; is "{Mission Title} - Fan Mission for ... - Thief Guild...".
    /// </summary>
    public static string ExtractDetailPageTitle(IDocument document) =>
        document.Title?.Split(" - ", 2).FirstOrDefault()?.Trim() ?? string.Empty;
}
