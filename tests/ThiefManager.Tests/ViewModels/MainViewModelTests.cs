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
        RecordingFolderDeleter? folderDeleter = null,
        FakeSeriesRepository? seriesRepo = null) =>
        new(repo, MakeLaunchService(exeExists), archiveInstaller ?? new RecordingArchiveInstaller(),
            folderDeleter ?? new RecordingFolderDeleter(), seriesRepo ?? new FakeSeriesRepository(repo));

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

    private static async Task<(FakeMissionRepository Repo, FakeSeriesRepository SeriesRepo, MainViewModel Vm)> MakeWithSeriesAsync()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByNameAsync("The Book of Prophecy");
        await repo.AddAsync(new FanMission { Title = "Part 3", Game = GameTitle.Thief2, FolderPath = "p3", SeriesId = series.Id, SeriesPosition = 3 });
        await repo.AddAsync(new FanMission { Title = "Part 2", Game = GameTitle.Thief2, FolderPath = "p2", SeriesId = series.Id, SeriesPosition = 2 });
        await repo.AddAsync(new FanMission { Title = "Alone", Game = GameTitle.Thief2, FolderPath = "a" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.SortField = SortField.Title;
        await vm.LoadCommand.ExecuteAsync(null);
        return (repo, seriesRepo, vm);
    }

    [Fact]
    public async Task Load_GroupsSeriesMembersUnderHeader()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();

        Assert.IsType<MissionRow>(vm.VisibleRows[0]);                       // Alone
        Assert.IsType<SeriesHeaderRow>(vm.VisibleRows[1]);                  // The Book of Prophecy
        Assert.Equal(new[] { "Alone", "Part 2", "Part 3" }, vm.VisibleMissions.Select(m => m.Title));
    }

    [Fact]
    public async Task SelectingSeriesHeader_ClearsSelectedMissionAndDisablesMissionCommands()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();
        vm.SelectedMission = vm.VisibleMissions.First(m => m.Title == "Part 2");

        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        Assert.Null(vm.SelectedMission);
        Assert.True(vm.IsSeriesHeaderSelected);
        Assert.False(vm.LaunchSelectedCommand.CanExecute(null));
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
        Assert.False(vm.SetSelectedStatusCommand.CanExecute(MissionStatus.Completed));
    }

    [Fact]
    public async Task SelectingMissionRow_SetsSelectedMission()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();
        var row = vm.VisibleRows.OfType<MissionRow>().First(r => r.Mission.Title == "Part 3");

        vm.SelectedRow = row;

        Assert.Same(row.Mission, vm.SelectedMission);
        Assert.False(vm.IsSeriesHeaderSelected);
    }

    [Fact]
    public async Task SetSelectedStatus_KeepsMissionSelected()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();
        vm.SelectedMission = vm.VisibleMissions.First(m => m.Title == "Part 2");

        await vm.SetSelectedStatusCommand.ExecuteAsync(MissionStatus.Completed);

        Assert.Equal("Part 2", vm.SelectedMission?.Title);
        Assert.Same(vm.SelectedMission, (vm.SelectedRow as MissionRow)?.Mission);
    }

    [Fact]
    public async Task ToggleSeriesExpanded_CollapsesAndPersists()
    {
        var (_, seriesRepo, vm) = await MakeWithSeriesAsync();
        var header = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        await vm.ToggleSeriesExpandedCommand.ExecuteAsync(header);

        Assert.False(seriesRepo.SeriesList.Single().IsExpanded);
        Assert.Equal(new[] { "Alone" }, vm.VisibleMissions.Select(m => m.Title));
        Assert.Single(vm.VisibleRows.OfType<SeriesHeaderRow>());
    }

    [Fact]
    public async Task ToggleSeriesExpanded_WithNullParameter_UsesSelectedHeader()
    {
        var (_, seriesRepo, vm) = await MakeWithSeriesAsync();
        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        await vm.ToggleSeriesExpandedCommand.ExecuteAsync(null);

        Assert.False(seriesRepo.SeriesList.Single().IsExpanded);
        Assert.True(vm.IsSeriesHeaderSelected); // header stays selected after the rebuild
    }

    [Fact]
    public async Task ToggleSeriesExpanded_CollapsingSelectedMembersSeries_SelectsItsHeader()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();
        var header = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();
        vm.SelectedMission = vm.VisibleMissions.First(m => m.Title == "Part 2");

        await vm.ToggleSeriesExpandedCommand.ExecuteAsync(header);

        var selectedHeader = Assert.IsType<SeriesHeaderRow>(vm.SelectedRow);
        Assert.Equal(header.Series.Id, selectedHeader.Series.Id);
        Assert.Null(vm.SelectedMission);
    }

    [Fact]
    public async Task RenameSeriesAsync_UpdatesHeader()
    {
        var (_, _, vm) = await MakeWithSeriesAsync();
        var header = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        await vm.RenameSeriesAsync(header.Series, "  Prophecy Saga ");

        Assert.Equal("Prophecy Saga", vm.VisibleRows.OfType<SeriesHeaderRow>().Single().Series.Name);
    }

    [Fact]
    public async Task UngroupSelectedSeries_DetachesMembersAndRemovesHeader()
    {
        var (repo, seriesRepo, vm) = await MakeWithSeriesAsync();
        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        await vm.UngroupSelectedSeriesCommand.ExecuteAsync(null);

        Assert.Empty(seriesRepo.SeriesList);
        Assert.All(repo.Missions, m => Assert.Null(m.SeriesId));
        Assert.Empty(vm.VisibleRows.OfType<SeriesHeaderRow>());
    }

    [Fact]
    public async Task DeleteSelected_LastMemberOfSeries_RemovesOrphanSeries()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByNameAsync("Solo Series");
        await repo.AddAsync(new FanMission { Title = "Only", FolderPath = "o", SeriesId = series.Id });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Empty(seriesRepo.SeriesList);
        Assert.Empty(vm.VisibleRows);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_WithSeries_AssignsSeriesAndGroups()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        await repo.AddAsync(new FanMission { Title = "Part 3", FolderPath = "p3" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadCommand.ExecuteAsync(null);
        var mission = vm.VisibleMissions.Single();

        await vm.ApplyThiefGuildMetadataAsync(mission, new ThiefGuildLookupResult(
            "Schattengilde", 2026, "City", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3)));

        Assert.Equal(3, repo.Missions.Single().SeriesPosition);
        Assert.True(repo.Missions.Single().SeriesLookupChecked);
        Assert.Equal("The Book of Prophecy", vm.VisibleRows.OfType<SeriesHeaderRow>().Single().Series.Name);
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

    [Fact]
    public void SortFieldDisplay_MapsNewSortFields()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.SortFieldDisplay = "TG Rating";
        Assert.Equal(SortField.ThiefGuildRating, vm.SortField);
        vm.SortField = SortField.MissionType;
        Assert.Equal("Type", vm.SortFieldDisplay);
        vm.SortField = SortField.InstallStatus;
        Assert.Equal("Install Status", vm.SortFieldDisplay);
        Assert.Contains("TG Rating", vm.SortFieldOptions);
    }
}
