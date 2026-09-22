using System.IO;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace ThiefManager.Services;

public class ArchiveInstaller : IArchiveInstaller
{
    public void Install(string archivePath, string destinationFolderPath)
    {
        Directory.CreateDirectory(destinationFolderPath);

        ArchiveFactory.WriteToDirectory(archivePath, destinationFolderPath, new ExtractionOptions
        {
            ExtractFullPath = true,
            Overwrite = true
        });
    }
}
