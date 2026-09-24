using System.IO;

namespace ThiefManager.Services;

public record DownloadedArchiveCandidate(string SuggestedTitle, string ArchivePath, string TargetFolderPath);

public static class DownloadedArchiveScanner
{
    public static IReadOnlyList<DownloadedArchiveCandidate> FindNewArchives(
        IEnumerable<string> archiveFilePaths,
        string fmFolderPath,
        IEnumerable<string> existingArchivePaths,
        IEnumerable<string>? existingInstalledNames = null,
        IEnumerable<string>? ignoredNames = null)
    {
        var existing = new HashSet<string>(existingArchivePaths, StringComparer.OrdinalIgnoreCase);
        var excludedNames = (existingInstalledNames ?? Enumerable.Empty<string>())
            .Concat(ignoredNames ?? Enumerable.Empty<string>())
            .Select(FmNameMatcher.Normalize)
            .Where(name => name.Length > 0)
            .ToHashSet();

        return archiveFilePaths
            .Where(path => !existing.Contains(path))
            .Select(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                var targetFolderPath = Path.Combine(fmFolderPath, baseName);
                return new DownloadedArchiveCandidate(baseName, path, targetFolderPath);
            })
            .Where(candidate => !excludedNames.Contains(FmNameMatcher.Normalize(candidate.SuggestedTitle)))
            .ToList();
    }
}
