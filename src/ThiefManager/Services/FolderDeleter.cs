using System.IO;

namespace ThiefManager.Services;

public class FolderDeleter : IFolderDeleter
{
    public void Delete(string folderPath)
    {
        if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, recursive: true);
    }
}
