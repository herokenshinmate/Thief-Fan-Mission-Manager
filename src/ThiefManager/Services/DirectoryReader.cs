using System.IO;

namespace ThiefManager.Services;

public class DirectoryReader : IDirectoryReader
{
    public IReadOnlyList<string> GetSubdirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();
}
