using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThiefManager.Services;

public static class ScanService
{
    public static IReadOnlyList<ScanCandidate> FindNewCandidates(
        IEnumerable<string> subfolderPaths,
        IEnumerable<string> existingFolderPaths)
    {
        var existing = new HashSet<string>(existingFolderPaths, StringComparer.OrdinalIgnoreCase);

        return subfolderPaths
            .Where(path => !existing.Contains(path))
            .Select(path =>
            {
                var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var title = Path.GetFileName(trimmed);
                return new ScanCandidate(title, path);
            })
            .ToList();
    }
}
