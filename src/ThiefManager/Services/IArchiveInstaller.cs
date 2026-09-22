namespace ThiefManager.Services;

public interface IArchiveInstaller
{
    void Install(string archivePath, string destinationFolderPath);
}
