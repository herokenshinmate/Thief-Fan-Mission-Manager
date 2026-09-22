namespace ThiefManager.Services;

public static class ThiefGuildTitleMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "in", "on", "at", "to", "and", "or", "for", "is",
        "between", "with", "from", "into", "part", "mission", "v1", "v2", "v3"
    };

    public static bool IsMatch(string candidateTitle, string searchTitle) =>
        Normalize(candidateTitle) == Normalize(searchTitle);

    private static string Normalize(string title) =>
        new(title.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// Thief Guild's search appears to do a fragile whole-word/stemmed match: searching a
    /// full word like "Hammered" finds nothing, while a short prefix like "Hammer" reliably
    /// finds it (and everything else containing that substring). Extracting the longest
    /// non-common word and truncating it to a short prefix maximizes recall; precision is
    /// then restored by filtering results through <see cref="IsMatch"/>.
    /// </summary>
    public static string ExtractSearchKeyword(string title)
    {
        var words = title.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var candidate = words
            .Where(w => !StopWords.Contains(w) && w.Length > 2)
            .OrderByDescending(w => w.Length)
            .FirstOrDefault() ?? title;

        return candidate.Length > 6 ? candidate[..6] : candidate;
    }
}
