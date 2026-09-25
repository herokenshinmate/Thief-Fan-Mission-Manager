using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace ThiefManager.Services;

public static class ThiefGuildPageParser
{
    private const string BaseUrl = "https://www.thiefguild.com";

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

        var currentTitle = scope is IDocument document ? ExtractDetailPageTitle(document) : null;
        var (rating, ratingCount) = ExtractRating(scope);

        return new ThiefGuildLookupResult(
            author, releaseYear, tags, url,
            ExtractSeries(scope, string.IsNullOrWhiteSpace(currentTitle) ? null : currentTitle),
            rating, ratingCount,
            ExtractCampaignMissionCount(scope),
            ExtractDescription(scope),
            ExtractSidebarLink(scope, "Sequel of:"),
            ExtractSidebarLink(scope, "FM has a sequel:"));
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
    /// The rating panel shows a star icon followed by the score ("9.<small>02</small>", so the
    /// paragraph's text reads "star 9.02") next to a "229 ratings" link to the rating list. Both
    /// are required; a missing half yields no rating rather than a misleading one.
    /// </summary>
    public static (double? Rating, int? Count) ExtractRating(IParentNode scope)
    {
        var countLink = scope.QuerySelector("a[href*='/fanmissions/rating_list/']");
        var countMatch = Regex.Match(countLink?.TextContent ?? string.Empty, @"(\d+)\s+ratings?");

        var starParagraph = scope.QuerySelectorAll("p")
            .FirstOrDefault(p => p.QuerySelectorAll("i.material-icons").Any(i => i.TextContent.Trim() == "star"));
        var ratingMatch = Regex.Match(starParagraph?.TextContent ?? string.Empty, @"(\d+(?:\.\d+)?)");

        if (!countMatch.Success || !ratingMatch.Success
            || !int.TryParse(countMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            || !double.TryParse(ratingMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rating))
            return (null, null);

        return (rating, count);
    }

    /// <summary>The sidebar says "Single mission" or "Campaign of N missions".</summary>
    public static int? ExtractCampaignMissionCount(IParentNode scope)
    {
        foreach (var item in scope.QuerySelectorAll("li.list-group-item"))
        {
            var text = NormalizeWhitespace(item.TextContent);
            if (text.Equals("Single mission", StringComparison.OrdinalIgnoreCase))
                return 1;

            var match = Regex.Match(text, @"^Campaign of (\d+) missions?$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                return count;
        }

        return null;
    }

    /// <summary>The page's schema.org JSON block carries the description as clean, unescaped text.</summary>
    public static string? ExtractDescription(IParentNode scope)
    {
        var script = scope.QuerySelector("script[type='application/ld+json']");
        if (script is null)
            return null;

        try
        {
            using var json = JsonDocument.Parse(script.TextContent);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String)
            {
                var text = description.GetString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>
    /// A sidebar item whose text starts with <paramref name="label"/> ("Sequel of:" or "FM has a
    /// sequel:") followed by a link to the related mission.
    /// </summary>
    public static ThiefGuildLink? ExtractSidebarLink(IParentNode scope, string label)
    {
        foreach (var item in scope.QuerySelectorAll("li.list-group-item"))
        {
            if (!NormalizeWhitespace(item.TextContent).StartsWith(label, StringComparison.OrdinalIgnoreCase))
                continue;

            var link = item.QuerySelector("a[href]");
            var title = link?.TextContent.Trim();
            var href = link?.GetAttribute("href");
            return string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href) ? null : new ThiefGuildLink(title, ToAbsoluteUrl(href));
        }

        return null;
    }

    private static string NormalizeWhitespace(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string ToAbsoluteUrl(string href) =>
        href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? href
            : BaseUrl + (href.StartsWith('/') ? href : "/" + href);

    /// <summary>
    /// A mission detail page's header &lt;h6&gt; links the series ("/fanmissions?series=66445", text
    /// "The Book of Prophecy:") and then lists every member in series order: the other missions as
    /// links, and the current mission as plain, unlinked text. The current mission's 1-based index in
    /// that list is its position. Search result cards have no such block. Anything not matching
    /// this shape (no series link, no or several unlinked entries) yields null rather than a guess.
    /// Every entry is also returned as a part: other missions titled from their link's tooltip minus the trailing year, the current one titled <paramref name="currentTitle"/> (or its abbreviation when not given).
    /// </summary>
    public static ThiefGuildSeriesInfo? ExtractSeries(IParentNode scope, string? currentTitle = null)
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
        var parts = new List<ThiefGuildSeriesPartInfo>();
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
                {
                    memberCount++;
                    var titleAttribute = element.GetAttribute("title");
                    var partTitle = string.IsNullOrWhiteSpace(titleAttribute)
                        ? element.TextContent.Trim()
                        : Regex.Replace(titleAttribute, @"\s*\(\d{4}\)\s*$", string.Empty).Trim();
                    parts.Add(new ThiefGuildSeriesPartInfo(memberCount, partTitle, ToAbsoluteUrl(href)));
                }
            }
            else if (node.NodeType == NodeType.Text && !string.IsNullOrWhiteSpace(node.TextContent))
            {
                memberCount++;
                if (currentPosition is not null)
                    return null;
                currentPosition = memberCount;
                parts.Add(new ThiefGuildSeriesPartInfo(memberCount, currentTitle ?? node.TextContent.Trim(), null));
            }
        }

        return currentPosition is int position ? new ThiefGuildSeriesInfo(seriesId, name, position, parts) : null;
    }
}
