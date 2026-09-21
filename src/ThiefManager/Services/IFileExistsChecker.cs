namespace ThiefManager.Services;

public interface IFileExistsChecker
{
    bool Exists(string path);
}
