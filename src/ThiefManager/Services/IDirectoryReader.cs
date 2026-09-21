namespace ThiefManager.Services;

public interface IDirectoryReader
{
    IReadOnlyList<string> GetSubdirectories(string path);
}
