using System.IO;

namespace ThiefManager.Services;

public record DownloadedArchiveCandidate(string SuggestedTitle, string ArchivePath, string TargetFolderPath);

public static class DownloadedArchiveScanner
{
    public static IReadOnlyList<DownloadedArchiveCandidate> FindNewArchives(
        IEnumerable<string> archiveFilePaths,
        string fmFolderPath,
        IEnumerable<string> existingArchivePaths)
    {
        var existing = new HashSet<string>(existingArchivePaths, StringComparer.OrdinalIgnoreCase);

        return archiveFilePaths
            .Where(path => !existing.Contains(path))
            .Select(path =>
            {
                var baseName = Path.GetFileNameWithoutExtension(path);
                var targetFolderPath = Path.Combine(fmFolderPath, baseName);
                return new DownloadedArchiveCandidate(baseName, path, targetFolderPath);
            })
            .ToList();
    }
}
