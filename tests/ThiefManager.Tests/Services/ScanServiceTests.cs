using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ScanServiceTests
{
    [Fact]
    public void FindNewCandidates_ExcludesFoldersAlreadyCataloged()
    {
        var subfolders = new[]
        {
            @"C:\fms\MissionA",
            @"C:\fms\MissionB"
        };
        var existing = new[] { @"C:\fms\MissionA" };

        var result = ScanService.FindNewCandidates(subfolders, existing);

        var candidate = Assert.Single(result);
        Assert.Equal(@"C:\fms\MissionB", candidate.FolderPath);
        Assert.Equal("MissionB", candidate.SuggestedTitle);
    }

    [Fact]
    public void FindNewCandidates_ComparisonIsCaseInsensitive()
    {
        var subfolders = new[] { @"C:\fms\MissionA" };
        var existing = new[] { @"c:\fms\missiona" };

        var result = ScanService.FindNewCandidates(subfolders, existing);

        Assert.Empty(result);
    }

    [Fact]
    public void FindNewCandidates_TrimsTrailingSlashWhenNamingSuggestedTitle()
    {
        var subfolders = new[] { @"C:\fms\MissionC\" };

        var result = ScanService.FindNewCandidates(subfolders, Array.Empty<string>());

        Assert.Equal("MissionC", Assert.Single(result).SuggestedTitle);
    }

    [Fact]
    public void FindNewCandidates_ExcludesFoldersStartingWithDot()
    {
        var subfolders = new[]
        {
            @"C:\fms\.fmsel.cache",
            @"C:\fms\MissionA"
        };

        var result = ScanService.FindNewCandidates(subfolders, Array.Empty<string>());

        var candidate = Assert.Single(result);
        Assert.Equal(@"C:\fms\MissionA", candidate.FolderPath);
    }

    [Fact]
    public void FindNewCandidates_ExcludesFoldersMatchingAnIgnoredName()
    {
        var subfolders = new[]
        {
            @"C:\fms\Ignored_Mission_v2",
            @"C:\fms\MissionA"
        };

        var result = ScanService.FindNewCandidates(subfolders, Array.Empty<string>(), ignoredNames: new[] { "Ignored Mission" });

        var candidate = Assert.Single(result);
        Assert.Equal(@"C:\fms\MissionA", candidate.FolderPath);
    }
}
