using ThiefManager.Models;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class MissionEditViewModelTests
{
    [Fact]
    public async Task SaveCommand_WithNoLoadedMission_AddsNewMissionToRepository()
    {
        var repo = new FakeMissionRepository();
        var vm = new MissionEditViewModel(repo)
        {
            Title = "New Mission",
            Game = GameTitle.Thief2,
            FolderPath = @"C:\fms\NewMission"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Equal("New Mission", saved.Title);
        Assert.Equal(GameTitle.Thief2, saved.Game);
    }

    [Fact]
    public async Task SaveCommand_AfterLoadFrom_UpdatesExistingMission()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Original", Game = GameTitle.Thief1, FolderPath = "p1" });
        var existing = repo.Missions.Single();
        var vm = new MissionEditViewModel(repo);
        vm.LoadFrom(existing);

        vm.Rating = 4;
        vm.Status = MissionStatus.Completed;
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Equal(4, saved.Rating);
        Assert.Equal(MissionStatus.Completed, saved.Status);
        Assert.Equal("Original", saved.Title);
    }

    [Fact]
    public async Task SaveCommand_RaisesSavedEvent()
    {
        var repo = new FakeMissionRepository();
        var vm = new MissionEditViewModel(repo) { Title = "X", FolderPath = "p" };
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
    }
}
