using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class DownloadedArchiveScannerTests
{
    [Fact]
    public void FindNewArchives_ExcludesArchivesAlreadyKnown()
    {
        var archives = new[] { @"C:\Downloads\MissionA.zip", @"C:\Downloads\MissionB.zip" };
        var existing = new[] { @"C:\Downloads\MissionA.zip" };

        var result = DownloadedArchiveScanner.FindNewArchives(archives, @"C:\fms", existing);

        var candidate = Assert.Single(result);
        Assert.Equal(@"C:\Downloads\MissionB.zip", candidate.ArchivePath);
    }

    [Fact]
    public void FindNewArchives_ComparisonIsCaseInsensitive()
    {
        var archives = new[] { @"C:\Downloads\MissionA.zip" };
        var existing = new[] { @"c:\downloads\missiona.zip" };

        var result = DownloadedArchiveScanner.FindNewArchives(archives, @"C:\fms", existing);

        Assert.Empty(result);
    }

    [Fact]
    public void FindNewArchives_ComputesTargetFolderPathAndTitleFromFileName()
    {
        var archives = new[] { @"C:\Downloads\A New Job.zip" };

        var result = DownloadedArchiveScanner.FindNewArchives(archives, @"C:\fms", Array.Empty<string>());

        var candidate = Assert.Single(result);
        Assert.Equal("A New Job", candidate.SuggestedTitle);
        Assert.Equal(Path.Combine(@"C:\fms", "A New Job"), candidate.TargetFolderPath);
    }
}
