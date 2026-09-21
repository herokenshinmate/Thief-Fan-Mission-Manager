using System.IO;

namespace ThiefManager.Services;

public class DirectoryReader : IDirectoryReader
{
    public IReadOnlyList<string> GetSubdirectories(string path)
    {
        if (!Directory.Exists(path))
            return Array.Empty<string>();

        return Directory.GetDirectories(path);
    }
}
