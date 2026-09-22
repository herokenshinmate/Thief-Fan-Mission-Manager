namespace ThiefManager.Services;

public interface IArchiveFileReader
{
    IReadOnlyList<string> GetArchiveFiles(string folderPath);
}
