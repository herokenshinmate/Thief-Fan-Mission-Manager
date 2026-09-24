using System.Text.RegularExpressions;

namespace ThiefManager.Services;

/// <summary>
/// Normalizes fan mission names so archives/folders that differ only in casing, punctuation,
/// or a version/tag suffix (e.g. "A New Job_v2" vs "A New Job (fixed)") can be matched against
/// an already-cataloged mission of the same underlying name.
/// </summary>
public static class FmNameMatcher
{
    private static readonly Regex BracketedContentRegex = new(@"[\(\[\{][^\)\]\}]*[\)\]\}]", RegexOptions.Compiled);
    private static readonly Regex WordSeparatorRegex = new(@"[_\-.]+", RegexOptions.Compiled);
    private static readonly Regex VersionSuffixRegex = new(@"\b(v|ver|version|rev|r)\s*\d+(\s+\d+)*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NonAlphanumericRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string Normalize(string name)
    {
        var value = name.ToLowerInvariant();
        value = BracketedContentRegex.Replace(value, " ");
        value = WordSeparatorRegex.Replace(value, " ");
        value = VersionSuffixRegex.Replace(value, " ");
        value = NonAlphanumericRegex.Replace(value, " ");
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool AreSimilar(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);
        return normalizedA.Length > 0 && normalizedA == normalizedB;
    }
}
