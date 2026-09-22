using System.IO;

namespace ThiefManager.Services;

public class ArchiveFileReader : IArchiveFileReader
{
    private static readonly string[] ArchiveExtensions = { ".zip", ".7z", ".rar" };

    public IReadOnlyList<string> GetArchiveFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return Array.Empty<string>();

        return Directory.GetFiles(folderPath)
            .Where(f => ArchiveExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }
}
