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
    private readonly Func<string, IReadOnlyList<string>>? _byFolder;

    public FakeArchiveFileReader(params string[] archiveFiles) => _archiveFiles = archiveFiles;
    public FakeArchiveFileReader(Func<string, IReadOnlyList<string>> byFolder)
    {
        _archiveFiles = Array.Empty<string>();
        _byFolder = byFolder;
    }

    public IReadOnlyList<string> GetArchiveFiles(string folderPath) => _byFolder?.Invoke(folderPath) ?? _archiveFiles;
}

public class ScanViewModelTests
{
    private static ScanViewModel MakeViewModel(
        FakeMissionRepository repo,
        IDirectoryReader? directoryReader = null,
        IArchiveFileReader? archiveFileReader = null,
        FakeIgnoredFmRepository? ignoredFmRepository = null) =>
        new(directoryReader ?? new FakeDirectoryReader(), archiveFileReader ?? new FakeArchiveFileReader(), repo, ignoredFmRepository ?? new FakeIgnoredFmRepository());

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

    [Fact]
    public async Task ScanDownloads_ForSecondGame_DoesNotClearFirstGamesCandidates()
    {
        var repo = new FakeMissionRepository();
        var archiveFileReader = new FakeArchiveFileReader(folder => folder == @"C:\Downloads1"
            ? new[] { @"C:\Downloads1\Thief1Mission.zip" }
            : new[] { @"C:\Downloads2\Thief2Mission.zip" });
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);

        await vm.ScanDownloads(GameTitle.Thief1, @"C:\Downloads1", @"C:\fms1");
        await vm.ScanDownloads(GameTitle.Thief2, @"C:\Downloads2", @"C:\fms2");

        Assert.Equal(2, vm.Candidates.Count);
        Assert.Contains(vm.Candidates, c => c.SuggestedTitle == "Thief1Mission" && c.Game == GameTitle.Thief1);
        Assert.Contains(vm.Candidates, c => c.SuggestedTitle == "Thief2Mission" && c.Game == GameTitle.Thief2);
    }

    [Fact]
    public async Task ImportSelectedCommand_WithCandidatesFromBothGames_TagsEachWithItsOwnGame()
    {
        var repo = new FakeMissionRepository();
        var archiveFileReader = new FakeArchiveFileReader(folder => folder == @"C:\Downloads1"
            ? new[] { @"C:\Downloads1\Thief1Mission.zip" }
            : new[] { @"C:\Downloads2\Thief2Mission.zip" });
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);
        await vm.ScanDownloads(GameTitle.Thief1, @"C:\Downloads1", @"C:\fms1");
        await vm.ScanDownloads(GameTitle.Thief2, @"C:\Downloads2", @"C:\fms2");
        foreach (var candidate in vm.Candidates)
            candidate.IsSelected = true;

        await vm.ImportSelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, repo.Missions.Count);
        Assert.Contains(repo.Missions, m => m.Title == "Thief1Mission" && m.Game == GameTitle.Thief1);
        Assert.Contains(repo.Missions, m => m.Title == "Thief2Mission" && m.Game == GameTitle.Thief2);
    }

    [Fact]
    public async Task ScanAllDownloads_CombinesResultsFromBothConfiguredGames()
    {
        var repo = new FakeMissionRepository();
        var archiveFileReader = new FakeArchiveFileReader(folder => folder == @"C:\Downloads1"
            ? new[] { @"C:\Downloads1\Thief1Mission.zip" }
            : new[] { @"C:\Downloads2\Thief2Mission.zip" });
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);
        var settings = new AppSettings
        {
            Thief1DownloadsFolder = @"C:\Downloads1",
            Thief1FmFolder = @"C:\fms1",
            Thief2DownloadsFolder = @"C:\Downloads2",
            Thief2FmFolder = @"C:\fms2"
        };

        await vm.ScanAllDownloads(settings);

        Assert.Equal(2, vm.Candidates.Count);
        Assert.Contains(vm.Candidates, c => c.SuggestedTitle == "Thief1Mission" && c.Game == GameTitle.Thief1);
        Assert.Contains(vm.Candidates, c => c.SuggestedTitle == "Thief2Mission" && c.Game == GameTitle.Thief2);
    }

    [Fact]
    public async Task ScanAllDownloads_SkipsGamesWithoutConfiguredFolders()
    {
        var repo = new FakeMissionRepository();
        var archiveFileReader = new FakeArchiveFileReader(@"C:\Downloads1\Thief1Mission.zip");
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader);
        var settings = new AppSettings
        {
            Thief1DownloadsFolder = @"C:\Downloads1",
            Thief1FmFolder = @"C:\fms1"
            // Thief2 folders left unconfigured
        };

        await vm.ScanAllDownloads(settings);

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal(GameTitle.Thief1, candidate.Game);
    }

    [Fact]
    public async Task Scan_ExcludesFoldersMatchingAnIgnoredName()
    {
        var repo = new FakeMissionRepository();
        var ignoredFmRepository = new FakeIgnoredFmRepository();
        await ignoredFmRepository.AddAsync(GameTitle.Thief1, "Ignored Mission");
        var directoryReader = new FakeDirectoryReader(@"C:\fms\Ignored_Mission_v2", @"C:\fms\NewOne");
        var vm = MakeViewModel(repo, directoryReader: directoryReader, ignoredFmRepository: ignoredFmRepository);

        await vm.Scan(GameTitle.Thief1, @"C:\fms");

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", candidate.SuggestedTitle);
    }

    [Fact]
    public async Task ScanDownloads_ExcludesArchivesMatchingAnIgnoredName()
    {
        var repo = new FakeMissionRepository();
        var ignoredFmRepository = new FakeIgnoredFmRepository();
        await ignoredFmRepository.AddAsync(GameTitle.Thief1, "Ignored Mission");
        var archiveFileReader = new FakeArchiveFileReader(@"C:\Downloads\Ignored Mission (repack).zip", @"C:\Downloads\NewOne.zip");
        var vm = MakeViewModel(repo, archiveFileReader: archiveFileReader, ignoredFmRepository: ignoredFmRepository);

        await vm.ScanDownloads(GameTitle.Thief1, @"C:\Downloads", @"C:\fms");

        var candidate = Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", candidate.SuggestedTitle);
    }

    [Fact]
    public async Task IgnoreCandidateCommand_AddsToIgnoreListAndRemovesMatchingCandidates()
    {
        var repo = new FakeMissionRepository();
        var ignoredFmRepository = new FakeIgnoredFmRepository();
        var directoryReader = new FakeDirectoryReader(@"C:\fms\SomeMission", @"C:\fms\NewOne");
        var vm = MakeViewModel(repo, directoryReader: directoryReader, ignoredFmRepository: ignoredFmRepository);
        await vm.Scan(GameTitle.Thief1, @"C:\fms");
        var toIgnore = vm.Candidates.Single(c => c.SuggestedTitle == "SomeMission");

        await vm.IgnoreCandidateCommand.ExecuteAsync(toIgnore);

        Assert.Single(vm.Candidates);
        Assert.Equal("NewOne", vm.Candidates.Single().SuggestedTitle);
        var ignored = Assert.Single(ignoredFmRepository.IgnoredFms);
        Assert.Equal("SomeMission", ignored.Name);
        Assert.Equal(GameTitle.Thief1, ignored.Game);
    }
}
