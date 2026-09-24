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
        public string? LastArguments;
        public void Start(string exePath, string? arguments = null)
        {
            LastStartedPath = exePath;
            LastArguments = arguments;
        }
    }

    private class StubFileExistsChecker : IFileExistsChecker
    {
        private readonly bool _exists;
        public StubFileExistsChecker(bool exists) => _exists = exists;
        public bool Exists(string path) => _exists;
    }

    private class RecordingArchiveInstaller : IArchiveInstaller
    {
        public string? LastArchivePath;
        public string? LastDestinationFolderPath;
        public bool ThrowOnInstall;

        public void Install(string archivePath, string destinationFolderPath)
        {
            if (ThrowOnInstall)
                throw new InvalidOperationException("boom");

            LastArchivePath = archivePath;
            LastDestinationFolderPath = destinationFolderPath;
        }
    }

    private class RecordingFolderDeleter : IFolderDeleter
    {
        public string? LastDeletedFolderPath;
        public bool ThrowOnDelete;

        public void Delete(string folderPath)
        {
            if (ThrowOnDelete)
                throw new InvalidOperationException("boom");

            LastDeletedFolderPath = folderPath;
        }
    }

    private static MainViewModel MakeViewModel(
        FakeMissionRepository repo,
        bool exeExists = true,
        RecordingArchiveInstaller? archiveInstaller = null,
        RecordingFolderDeleter? folderDeleter = null) =>
        new(repo, MakeLaunchService(exeExists), archiveInstaller ?? new RecordingArchiveInstaller(), folderDeleter ?? new RecordingFolderDeleter());

    [Fact]
    public async Task LoadCommand_PopulatesVisibleMissionsFromRepository()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Z", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "A", Game = GameTitle.Thief1, FolderPath = "p2" });
        var vm = MakeViewModel(repo);
        vm.SortField = SortField.Title;

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "A", "Z" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task LoadCommand_DefaultsToSortingByGameWithThief1First()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "T2 Mission", Game = GameTitle.Thief2, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "T1 Mission", Game = GameTitle.Thief1, FolderPath = "p2" });
        var vm = MakeViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(SortField.Game, vm.SortField);
        Assert.True(vm.SortAscending);
        Assert.Equal(new[] { "T1 Mission", "T2 Mission" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task SettingGameFilter_NarrowsVisibleMissions()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "T1 Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "T2 Mission", Game = GameTitle.Thief2, FolderPath = "p2" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.GameFilter = GameTitle.Thief2;

        Assert.Equal(new[] { "T2 Mission" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task SettingInstallStatusFilter_NarrowsVisibleMissions()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Installed", Game = GameTitle.Thief1, FolderPath = "p1", InstallStatus = InstallStatus.Installed });
        await repo.AddAsync(new FanMission { Title = "NotInstalled", Game = GameTitle.Thief1, FolderPath = "p2", InstallStatus = InstallStatus.NotInstalled });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.InstallStatusFilter = InstallStatus.NotInstalled;

        Assert.Equal(new[] { "NotInstalled" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task LaunchSelectedCommand_WithNoExeConfigured_SetsLaunchError()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = MakeViewModel(repo, exeExists: false);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.LaunchSelectedCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(vm.LaunchError));
    }

    [Fact]
    public async Task LaunchSelectedCommand_CanExecute_FalseWhenNotInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1", InstallStatus = InstallStatus.NotInstalled });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        Assert.False(vm.LaunchSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteSelectedCommand_RemovesMissionFromRepositoryAndList()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = MakeViewModel(repo);
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
        var vm = MakeViewModel(repo);
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
        var vm = MakeViewModel(repo);

        Assert.False(vm.SetSelectedStatusCommand.CanExecute(MissionStatus.Completed));
    }

    [Fact]
    public async Task InstallSelectedCommand_ExtractsArchiveAndFlipsToInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission
        {
            Title = "Mission",
            Game = GameTitle.Thief1,
            FolderPath = @"C:\fms\Mission",
            ArchivePath = @"C:\Downloads\Mission.zip",
            InstallStatus = InstallStatus.NotInstalled
        });
        var archiveInstaller = new RecordingArchiveInstaller();
        var vm = MakeViewModel(repo, archiveInstaller: archiveInstaller);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.InstallSelectedCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Downloads\Mission.zip", archiveInstaller.LastArchivePath);
        Assert.Equal(@"C:\fms\Mission", archiveInstaller.LastDestinationFolderPath);
        Assert.Equal(InstallStatus.Installed, vm.VisibleMissions.Single().InstallStatus);
        Assert.Equal(InstallStatus.Installed, (await repo.GetAllAsync()).Single().InstallStatus);
        Assert.Null(vm.InstallError);
    }

    [Fact]
    public async Task InstallSelectedCommand_WhenExtractionThrows_SetsInstallErrorAndKeepsNotInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission
        {
            Title = "Mission",
            Game = GameTitle.Thief1,
            FolderPath = @"C:\fms\Mission",
            ArchivePath = @"C:\Downloads\Mission.zip",
            InstallStatus = InstallStatus.NotInstalled
        });
        var archiveInstaller = new RecordingArchiveInstaller { ThrowOnInstall = true };
        var vm = MakeViewModel(repo, archiveInstaller: archiveInstaller);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.InstallSelectedCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.InstallError));
        Assert.Equal(InstallStatus.NotInstalled, vm.VisibleMissions.Single().InstallStatus);
    }

    [Fact]
    public async Task InstallSelectedCommand_CanExecute_FalseWithoutArchivePath()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1", InstallStatus = InstallStatus.NotInstalled, ArchivePath = null });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        Assert.False(vm.InstallSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task UninstallSelectedCommand_DeletesFolderAndFlipsToNotInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Mission", InstallStatus = InstallStatus.Installed });
        var folderDeleter = new RecordingFolderDeleter();
        var vm = MakeViewModel(repo, folderDeleter: folderDeleter);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\fms\Mission", folderDeleter.LastDeletedFolderPath);
        Assert.Equal(InstallStatus.NotInstalled, vm.VisibleMissions.Single().InstallStatus);
        Assert.Equal(InstallStatus.NotInstalled, (await repo.GetAllAsync()).Single().InstallStatus);
    }

    [Fact]
    public async Task UninstallSelectedCommand_WhenDeleteThrows_SetsInstallErrorAndKeepsInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = @"C:\fms\Mission", InstallStatus = InstallStatus.Installed });
        var folderDeleter = new RecordingFolderDeleter { ThrowOnDelete = true };
        var vm = MakeViewModel(repo, folderDeleter: folderDeleter);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.False(string.IsNullOrEmpty(vm.InstallError));
        Assert.Equal(InstallStatus.Installed, vm.VisibleMissions.Single().InstallStatus);
    }

    [Fact]
    public async Task UninstallSelectedCommand_CanExecute_FalseWhenNotInstalled()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1", InstallStatus = InstallStatus.NotInstalled });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        Assert.False(vm.UninstallSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_FillsOnlyBlankFieldsAndPersists()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1", Author = "Existing Author" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var mission = vm.VisibleMissions.Single();
        var result = new ThiefGuildLookupResult("Scraped Author", 2020, "Church, City", "https://www.thiefguild.com/fanmissions/1/mission");

        await vm.ApplyThiefGuildMetadataAsync(mission, result);

        var saved = (await repo.GetAllAsync()).Single();
        Assert.Equal("Existing Author", saved.Author);
        Assert.Equal(2020, saved.ReleaseYear);
        Assert.Equal("Church, City", saved.Tags);
        Assert.Equal("https://www.thiefguild.com/fanmissions/1/mission", saved.ThiefGuildUrl);
    }

    [Fact]
    public async Task DismissThiefGuildLookupAsync_PersistsDismissedFlag()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var mission = vm.VisibleMissions.Single();

        await vm.DismissThiefGuildLookupAsync(mission);

        var saved = (await repo.GetAllAsync()).Single();
        Assert.True(saved.ThiefGuildLookupDismissed);
    }
}
