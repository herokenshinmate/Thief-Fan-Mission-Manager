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

public class ScanViewModelTests
{
    [Fact]
    public async Task Scan_PopulatesCandidatesExcludingAlreadyCatalogedFolders()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Existing", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Existing" });
        var directoryReader = new FakeDirectoryReader(@"C:\fms\Existing", @"C:\fms\NewOne");
        var vm = new ScanViewModel(directoryReader, repo);

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
        var vm = new ScanViewModel(directoryReader, repo);
        await vm.Scan(GameTitle.Thief2, @"C:\fms");
        vm.Candidates.Single(c => c.SuggestedTitle == "NewOne").IsSelected = true;

        await vm.ImportSelectedCommand.ExecuteAsync(null);

        var imported = Assert.Single(repo.Missions);
        Assert.Equal("NewOne", imported.Title);
        Assert.Equal(GameTitle.Thief2, imported.Game);
        Assert.Single(vm.Candidates);
        Assert.Equal("NewTwo", vm.Candidates.Single().SuggestedTitle);
    }
}
