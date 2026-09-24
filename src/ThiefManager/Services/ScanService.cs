using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThiefManager.Services;

public static class ScanService
{
    public static IReadOnlyList<ScanCandidate> FindNewCandidates(
        IEnumerable<string> subfolderPaths,
        IEnumerable<string> existingFolderPaths,
        IEnumerable<string>? ignoredNames = null)
    {
        var existing = new HashSet<string>(existingFolderPaths, StringComparer.OrdinalIgnoreCase);
        var ignored = (ignoredNames ?? Enumerable.Empty<string>())
            .Select(FmNameMatcher.Normalize)
            .Where(name => name.Length > 0)
            .ToHashSet();

        return subfolderPaths
            .Where(path => !existing.Contains(path))
            .Where(path => !IsDotFolder(path))
            .Select(path =>
            {
                var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var title = Path.GetFileName(trimmed);
                return new ScanCandidate(title, path);
            })
            .Where(candidate => !ignored.Contains(FmNameMatcher.Normalize(candidate.SuggestedTitle)))
            .ToList();
    }

    private static bool IsDotFolder(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed).StartsWith('.');
    }
}
