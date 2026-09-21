namespace ThiefManager.Services;

public class FileExistsChecker : IFileExistsChecker
{
    public bool Exists(string path) => System.IO.File.Exists(path);
}
