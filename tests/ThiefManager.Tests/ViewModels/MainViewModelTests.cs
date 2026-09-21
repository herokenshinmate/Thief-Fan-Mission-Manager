using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class MainViewModelTests
{
    private static LaunchService MakeLaunchService(bool exeExists) =>
        new(new RecordingProcessLauncher(), new StubFileExistsChecker(exeExists));

    private class RecordingProcessLauncher : IProcessLauncher
    {
        public string? LastStartedPath;
        public void Start(string exePath) => LastStartedPath = exePath;
    }

    private class StubFileExistsChecker : IFileExistsChecker
    {
        private readonly bool _exists;
        public StubFileExistsChecker(bool exists) => _exists = exists;
        public bool Exists(string path) => _exists;
    }

    [Fact]
    public async Task LoadCommand_PopulatesVisibleMissionsFromRepository()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Z", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "A", Game = GameTitle.Thief1, FolderPath = "p2" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "A", "Z" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task SettingGameFilter_NarrowsVisibleMissions()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "T1 Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "T2 Mission", Game = GameTitle.Thief2, FolderPath = "p2" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));
        await vm.LoadCommand.ExecuteAsync(null);

        vm.GameFilter = GameTitle.Thief2;

        Assert.Equal(new[] { "T2 Mission" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task LaunchSelectedCommand_WithNoExeConfigured_SetsLaunchError()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = new MainViewModel(repo, MakeLaunchService(false));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.LaunchSelectedCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.LaunchError));
    }

    [Fact]
    public async Task DeleteSelectedCommand_RemovesMissionFromRepositoryAndList()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(vm.VisibleMissions);
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task SetSelectedStatusCommand_UpdatesStatusInRepositoryAndVisibleMissions()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = new MainViewModel(repo, MakeLaunchService(true));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.SetSelectedStatusCommand.ExecuteAsync(MissionStatus.Completed);

        Assert.Equal(MissionStatus.Completed, vm.VisibleMissions.Single().Status);
        Assert.Equal(MissionStatus.Completed, (await repo.GetAllAsync()).Single().Status);
    }

    [Fact]
    public void SetSelectedStatusCommand_CanExecute_FalseWithoutSelection()
    {
        var repo = new FakeMissionRepository();
        var vm = new MainViewModel(repo, MakeLaunchService(true));

        Assert.False(vm.SetSelectedStatusCommand.CanExecute(MissionStatus.Completed));
    }
}
