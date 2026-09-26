using System.Text.RegularExpressions;

namespace ThiefManager.Services;

public static class ThiefGuildTitleMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "in", "on", "at", "to", "and", "or", "for", "is",
        "between", "with", "from", "into", "part", "mission"
    };

    private static readonly Regex VersionWordRegex = new(@"^(v|ver|version|rev|r)\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // \b treats '_' as a word character, so it wouldn't find "rev3" right after "..._rev3", and a
    // plain \d+ can't span a dotted version like "v1.1". Lookarounds against a preceding/following
    // letter fix the boundary, and allowing '.', '_', '-', or space between digit groups covers
    // "v1.1", "v1_1", and "rev 3" alike.
    private static readonly Regex VersionSuffixRegex = new(
        @"(?<![A-Za-z])(v|ver|version|rev|r)[\s_.-]*\d+(?:[\s_.-]+\d+)*(?![A-Za-z])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Splits a run of words with no separator between them (e.g. "AmbushInTheDark") into
    // individual words at each lowercase-to-uppercase boundary, so they can be matched like any
    // other multi-word title.
    private static readonly Regex CamelCaseBoundaryRegex = new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    public static bool IsMatch(string candidateTitle, string searchTitle) =>
        Normalize(candidateTitle) == Normalize(searchTitle);

    /// <summary>
    /// Local archive/folder names often carry a version tag Thief Guild's own title never has
    /// (e.g. "Ambush In The Dark_v2"), so it's stripped before comparing either side.
    /// </summary>
    private static string Normalize(string title)
    {
        var value = VersionSuffixRegex.Replace(title.ToLowerInvariant(), " ");
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>
    /// Thief Guild's search appears to do a fragile whole-word/stemmed match: searching a
    /// full word like "Hammered" finds nothing, while a short prefix like "Hammer" reliably
    /// finds it (and everything else containing that substring). Extracting the longest
    /// non-common word and truncating it to a short prefix maximizes recall; precision is
    /// then restored by filtering results through <see cref="IsMatch"/>.
    /// </summary>
    public static string ExtractSearchKeyword(string title)
    {
        var expanded = CamelCaseBoundaryRegex.Replace(title, " ");
        var words = expanded.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var candidate = words
            .Where(w => !StopWords.Contains(w) && !VersionWordRegex.IsMatch(w) && w.Length > 2)
            .OrderByDescending(w => w.Length)
            .FirstOrDefault() ?? title;

        return candidate.Length > 6 ? candidate[..6] : candidate;
    }
}
