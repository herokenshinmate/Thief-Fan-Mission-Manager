using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class FakeDirectoryReader : IDirectoryReader
{
    private readonly IReadOnlyList<string> _subdirectories;
    public FakeDirectoryReader(params string[] subdirectories) => _subdirectories = subdirectories;
    public IReadOnlyList<string> GetSubdirectories(string path) => _subdirectories;
}

public class FakeArchiveFileReader : IArchiveFileReader
{
    private readonly IReadOnlyList<string> _archiveFiles;
    public FakeArchiveFileReader(params string[] archiveFiles) => _archiveFiles = archiveFiles;
    public IReadOnlyList<string> GetArchiveFiles(string folderPath) => _archiveFiles;
}

public class ScanViewModelTests
{
    private static ScanViewModel MakeViewModel(
        FakeMissionRepository repo,
        IDirectoryReader? directoryReader = null,
        IArchiveFileReader? archiveFileReader = null) =>
        new(directoryReader ?? new FakeDirectoryReader(), archiveFileReader ?? new FakeArchiveFileReader(), repo);

    [Fact]
    public async Task Scan_PopulatesCandidatesExcludingAlreadyCatalogedFolders()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Existing", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Existing" });
        var directoryReader = new FakeDirectoryReader(@"C:\fms\Existing", @"C:\fms\NewOne");
        var vm = MakeViewModel(repo, directoryReader: directoryReader);

        await vm.Scan(GameTitle.Thief1, @"C:\fms");

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", candidate.SuggestedTitle);
        Assert.Equal(@"C:\fms\NewOne", candidate.FolderPath);
    }

    [Fact]
    public async Task ImportSelectedCommand_AddsCheckedCandidatesToRepositoryAndClearsThem()
    {
        var repo = new FakeMissionRepository();
        var directoryReader = new FakeDirectoryReader(@"C:\fms\NewOne", @"C:\fms\NewTwo");
        var vm = MakeViewModel(repo, directoryReader: directoryReader);
        await vm.Scan(GameTitle.Thief2, @"C:\fms");
        vm.Candidates.Single(c => c.SuggestedTitle == "NewOne").IsSelected = true;

        await vm.ImportSelectedCommand.ExecuteAsync(null);

        var imported = Assert.Single(repo.Missions);
        Assert.Equal("NewOne", imported.Title);
        Assert.Equal(GameTitle.Thief2, imported.Game);
        Assert.Equal(InstallStatus.Installed, imported.InstallStatus);
        Assert.Null(imported.ArchivePath);
        Assert.Single(vm.Candidates);
        Assert.Equal("NewTwo", vm.Candidates.Single().SuggestedTitle);
    }

    [Fact]
    public async Task ScanDownloads_PopulatesCandidatesExcludingAlreadyKnownArchives()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Existing", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Existing", ArchivePath = @"C:\Downloads\Existing.zip" });
        var archiveFileReader = new FakeArchiveFileReader(@"C:\Downloads\Existing.zip", @"C:\Downloads\NewOne.zip");
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);

        await vm.ScanDownloads(GameTitle.Thief1, @"C:\Downloads", @"C:\fms");

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", candidate.SuggestedTitle);
        Assert.Equal(@"C:\Downloads\NewOne.zip", candidate.ArchivePath);
        Assert.Equal(Path.Combine(@"C:\fms", "NewOne"), candidate.FolderPath);
    }

    [Fact]
    public async Task ImportSelectedCommand_ForDownloadedArchiveCandidate_AddsAsNotInstalledWithArchivePath()
    {
        var repo = new FakeMissionRepository();
        var archiveFileReader = new FakeArchiveFileReader(@"C:\Downloads\NewOne.zip");
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);
        await vm.ScanDownloads(GameTitle.Thief1, @"C:\Downloads", @"C:\fms");
        vm.Candidates.Single().IsSelected = true;

        await vm.ImportSelectedCommand.ExecuteAsync(null);

        var imported = Assert.Single(repo.Missions);
        Assert.Equal(InstallStatus.NotInstalled, imported.InstallStatus);
        Assert.Equal(@"C:\Downloads\NewOne.zip", imported.ArchivePath);
        Assert.Equal(Path.Combine(@"C:\fms", "NewOne"), imported.FolderPath);
    }
}
