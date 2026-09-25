using System.Net.Http;
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
        FakeSeriesRepository? seriesRepo = null,
        FakeSettingsRepository? settingsRepo = null,
        FakeUpdateService? updateService = null) =>
        new(repo, MakeLaunchService(exeExists), archiveInstaller ?? new RecordingArchiveInstaller(),
            folderDeleter ?? new RecordingFolderDeleter(), seriesRepo ?? new FakeSeriesRepository(repo),
            settingsRepo ?? new FakeSettingsRepository(), updateService ?? new FakeUpdateService { IsInstalled = false });

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
    public async Task LoadCommand_DefaultsToTitleSortWithinEachGameBanner()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Zeal", Game = GameTitle.Thief2, FolderPath = "p1" });
        await repo.AddAsync(new FanMission { Title = "Ashes", Game = GameTitle.Thief2, FolderPath = "p2" });
        await repo.AddAsync(new FanMission { Title = "T1 Mission", Game = GameTitle.Thief1, FolderPath = "p3" });
        var vm = MakeViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(SortField.Title, vm.SortField);
        Assert.True(vm.SortAscending);
        Assert.Equal(new[] { "T1 Mission", "Ashes", "Zeal" }, vm.VisibleMissions.Select(m => m.Title));
        Assert.DoesNotContain("Game", vm.SortFieldOptions);
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

        Assert.IsType<GameHeaderRow>(vm.VisibleRows[0]);
        Assert.IsType<MissionRow>(vm.VisibleRows[1]);                       // Alone
        Assert.IsType<SeriesHeaderRow>(vm.VisibleRows[2]);                  // The Book of Prophecy
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

    private static async Task<(FakeMissionRepository Repo, FakeSeriesRepository SeriesRepo, MainViewModel Vm)> MakeWithPartsAsync()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");
        await seriesRepo.ReplacePartsAsync(series.Id, new[]
        {
            new SeriesPart { Position = 1, Title = "Dead Letter Box", ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2684/p1" },
            new SeriesPart { Position = 2, Title = "The Hidden City" }
        });
        await repo.AddAsync(new FanMission { Title = "The Hidden City", Game = GameTitle.Thief2, FolderPath = "p2", SeriesId = series.Id, SeriesPosition = 2, ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2682/p2" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.SortField = SortField.Title;
        await vm.LoadCommand.ExecuteAsync(null);
        return (repo, seriesRepo, vm);
    }

    [Fact]
    public async Task Load_ShowsMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        Assert.IsType<GameHeaderRow>(vm.VisibleRows[0]);
        Assert.IsType<SeriesHeaderRow>(vm.VisibleRows[1]);
        Assert.Equal("Dead Letter Box", Assert.IsType<MissingPartRow>(vm.VisibleRows[2]).Part.Title);
        Assert.IsType<MissionRow>(vm.VisibleRows[3]);
    }

    [Fact]
    public async Task StatusFilter_HidesMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.StatusFilter = MissionStatus.NotPlayed;

        Assert.Empty(vm.VisibleRows.OfType<MissingPartRow>());
    }

    [Fact]
    public async Task GameFilter_KeepsMissingPartPlaceholders()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.GameFilter = GameTitle.Thief2;

        Assert.Single(vm.VisibleRows.OfType<MissingPartRow>());
    }

    [Fact]
    public async Task SelectingMissingPart_DisablesMissionCommandsAndOffersItsThiefGuildPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.SelectedRow = vm.VisibleRows.OfType<MissingPartRow>().Single();

        Assert.Null(vm.SelectedMission);
        Assert.True(vm.IsMissingPartSelected);
        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
        Assert.True(vm.CanOpenSelectedOnThiefGuild);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2684/p1", vm.SelectedThiefGuildUrl);
    }

    [Fact]
    public async Task SelectingMissionRow_OffersItsThiefGuildPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.SelectedMission = vm.VisibleMissions.Single();

        Assert.True(vm.ShowMissionMenuItems);
        Assert.Equal("https://www.thiefguild.com/fanmissions/2682/p2", vm.SelectedThiefGuildUrl);
        Assert.False(vm.CanOpenSelectedSeriesOnThiefGuild);
    }

    [Fact]
    public async Task SelectingThiefGuildSeriesHeader_OffersSeriesPage()
    {
        var (_, _, vm) = await MakeWithPartsAsync();

        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single();

        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.CanOpenSelectedOnThiefGuild);
        Assert.True(vm.CanOpenSelectedSeriesOnThiefGuild);
        Assert.Equal("https://www.thiefguild.com/fanmissions?series=66445", vm.SelectedSeriesThiefGuildUrl);
    }

    [Fact]
    public void IsThiefGuildRefreshRunning_TogglesCanRefresh()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.IsThiefGuildRefreshRunning = true;

        Assert.False(vm.CanRefreshThiefGuild);
    }

    [Fact]
    public async Task ApplyThiefGuildMetadataAsync_StoresMetadataAndParts()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        await repo.AddAsync(new FanMission { Title = "In the Lion's Den", FolderPath = "p3" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ApplyThiefGuildMetadataAsync(vm.VisibleMissions.Single(), new ThiefGuildLookupResult(
            "Schattengilde", 2026, "City", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3, new[]
            {
                new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
                new ThiefGuildSeriesPartInfo(2, "The Hidden City", "https://www.thiefguild.com/fanmissions/2682/p2"),
                new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
            }),
            Rating: 8.5, RatingCount: 12, CampaignMissionCount: 1));

        var mission = repo.Missions.Single();
        Assert.Equal(8.5, mission.ThiefGuildRating);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
        Assert.Equal(2, vm.VisibleRows.OfType<MissingPartRow>().Count());
    }

    [Fact]
    public async Task ApplyFetchedThiefGuildMetadata_PatchesLoadedMissionWithoutRebuilding()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var loaded = vm.VisibleMissions.Single();
        var rowBefore = vm.VisibleRows.OfType<MissionRow>().Single();
        var fetched = new FanMission
        {
            Id = loaded.Id,
            ThiefGuildRating = 8.5,
            Description = "A rainy night.",
            ThiefGuildMetadataVersion = 2
        };

        var result = vm.ApplyFetchedThiefGuildMetadata(fetched);

        Assert.False(result);
        Assert.Equal(8.5, vm.VisibleMissions.Single().ThiefGuildRating);
        Assert.Equal(2, vm.VisibleMissions.Single().ThiefGuildMetadataVersion);
        Assert.Same(rowBefore, vm.VisibleRows.OfType<MissionRow>().Single());
    }

    [Fact]
    public async Task ApplyFetchedThiefGuildMetadata_NewSeries_RequestsRebuild()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var loaded = vm.VisibleMissions.Single();
        var fetched = new FanMission { Id = loaded.Id, SeriesId = 5, SeriesPosition = 2, SeriesLookupChecked = true };

        var result = vm.ApplyFetchedThiefGuildMetadata(fetched);

        Assert.True(result);
        Assert.Equal(5, vm.VisibleMissions.Single().SeriesId);
    }

    [Fact]
    public async Task ApplyFetchedThiefGuildMetadata_DoesNotRegroupUngroupedMission()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "Mission", Game = GameTitle.Thief1, FolderPath = "p1", SeriesLookupChecked = true });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var loaded = vm.VisibleMissions.Single();
        var fetched = new FanMission { Id = loaded.Id, SeriesId = 5 };

        var result = vm.ApplyFetchedThiefGuildMetadata(fetched);

        Assert.False(result);
        Assert.Null(vm.VisibleMissions.Single().SeriesId);
    }

    [Fact]
    public async Task SelectingGameBanner_DisablesMissionCommandsAndHidesMissionMenu()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        vm.SelectedRow = vm.VisibleRows.OfType<GameHeaderRow>().Single();

        Assert.Null(vm.SelectedMission);
        Assert.True(vm.IsGameHeaderSelected);
        Assert.False(vm.ShowMissionMenuItems);
        Assert.False(vm.DeleteSelectedCommand.CanExecute(null));
    }

    [Fact]
    public async Task ToggleGameExpanded_CollapsesAndPersists()
    {
        var repo = new FakeMissionRepository();
        var settings = new FakeSettingsRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo, settingsRepo: settings);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ToggleGameExpandedCommand.ExecuteAsync(vm.VisibleRows.OfType<GameHeaderRow>().Single());

        Assert.Empty(vm.VisibleMissions);
        Assert.False(vm.VisibleRows.OfType<GameHeaderRow>().Single().IsExpanded);
        Assert.True((await settings.GetAsync()).Thief1Collapsed);
    }

    [Fact]
    public async Task Load_AppliesCollapsedGamesFromSettings()
    {
        var repo = new FakeMissionRepository();
        var settings = new FakeSettingsRepository();
        await settings.SetGameCollapsedAsync(GameTitle.Thief2, true);
        await repo.AddAsync(new FanMission { Title = "T1", Game = GameTitle.Thief1, FolderPath = "a" });
        await repo.AddAsync(new FanMission { Title = "T2", Game = GameTitle.Thief2, FolderPath = "b" });
        var vm = MakeViewModel(repo, settingsRepo: settings);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "T1" }, vm.VisibleMissions.Select(m => m.Title));
        Assert.Equal(2, vm.VisibleRows.OfType<GameHeaderRow>().Count());
    }

    [Fact]
    public async Task CollapsingSelectedMissionsGame_SelectsItsBanner()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.ToggleGameExpandedCommand.ExecuteAsync(vm.VisibleRows.OfType<GameHeaderRow>().Single());

        Assert.Equal(GameTitle.Thief1, Assert.IsType<GameHeaderRow>(vm.SelectedRow).Game);
        Assert.Null(vm.SelectedMission);
    }

    [Fact]
    public async Task StatusChangeHidingSelectedMission_ClearsSelectionRatherThanSelectingBanner()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m", Status = MissionStatus.NotPlayed });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.StatusFilter = MissionStatus.NotPlayed;
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.SetSelectedStatusCommand.ExecuteAsync(MissionStatus.Completed);

        Assert.Null(vm.SelectedRow);
    }

    [Fact]
    public async Task ReselectingSpanningSeriesHeader_KeepsTheSameGamesHeader()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByNameAsync("Spanning Series");
        await repo.AddAsync(new FanMission { Title = "T1 Part", Game = GameTitle.Thief1, FolderPath = "p1", SeriesId = series.Id, SeriesPosition = 1 });
        await repo.AddAsync(new FanMission { Title = "T2 Part", Game = GameTitle.Thief2, FolderPath = "p2", SeriesId = series.Id, SeriesPosition = 2 });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedRow = vm.VisibleRows.OfType<SeriesHeaderRow>().Single(h => h.CommonGame == GameTitle.Thief2);

        vm.SortAscending = !vm.SortAscending;

        var selectedHeader = Assert.IsType<SeriesHeaderRow>(vm.SelectedRow);
        Assert.Equal(GameTitle.Thief2, selectedHeader.CommonGame);
    }

    [Fact]
    public async Task SelectedGameBanner_SurvivesSortChange()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedRow = vm.VisibleRows.OfType<GameHeaderRow>().Single(g => g.Game == GameTitle.Thief1);

        vm.SortAscending = !vm.SortAscending;

        Assert.Equal(GameTitle.Thief1, Assert.IsType<GameHeaderRow>(vm.SelectedRow).Game);
    }

    [Fact]
    public async Task DeleteSelected_InstalledMission_DeletesFolderThenRemovesFromLibrary()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "fms/m", InstallStatus = InstallStatus.Installed });
        var deleter = new RecordingFolderDeleter();
        var vm = MakeViewModel(repo, folderDeleter: deleter);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Equal("fms/m", deleter.LastDeletedFolderPath);
        Assert.Empty(repo.Missions);
    }

    [Fact]
    public async Task DeleteSelected_NotInstalledMission_LeavesDiskAlone()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "fms/m", InstallStatus = InstallStatus.NotInstalled });
        var deleter = new RecordingFolderDeleter();
        var vm = MakeViewModel(repo, folderDeleter: deleter);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Null(deleter.LastDeletedFolderPath);
        Assert.Empty(repo.Missions);
    }

    [Fact]
    public async Task DeleteSelected_WhenFolderDeleteFails_KeepsMissionAndReportsError()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "fms/m", InstallStatus = InstallStatus.Installed });
        var vm = MakeViewModel(repo, folderDeleter: new RecordingFolderDeleter { ThrowOnDelete = true });
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Single(repo.Missions);
        Assert.Single(vm.VisibleMissions);
        Assert.StartsWith("Failed to delete", vm.InstallError);
    }

    [Fact]
    public async Task SetSelectedRating_SetsAndClearsRating()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "M", Game = GameTitle.Thief1, FolderPath = "m" });
        var vm = MakeViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedMission = vm.VisibleMissions.Single();

        await vm.SetSelectedRatingCommand.ExecuteAsync(4);
        Assert.Equal(4, repo.Missions.Single().Rating);

        await vm.SetSelectedRatingCommand.ExecuteAsync(null);
        Assert.Null(repo.Missions.Single().Rating);
    }

    [Fact]
    public void SetSelectedRating_IsDisabledWithoutASelectedMission()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        Assert.False(vm.SetSelectedRatingCommand.CanExecute(3));
    }

    [Fact]
    public async Task CollapseAllSeries_CollapsesEverySeriesButKeepsGamesOpen()
    {
        var (_, seriesRepo, vm) = await MakeWithSeriesAsync();

        await vm.CollapseAllSeriesCommand.ExecuteAsync(null);

        Assert.All(seriesRepo.SeriesList, s => Assert.False(s.IsExpanded));
        Assert.Equal(new[] { "Alone" }, vm.VisibleMissions.Select(m => m.Title));
        Assert.All(vm.VisibleRows.OfType<GameHeaderRow>(), g => Assert.True(g.IsExpanded));
    }

    [Fact]
    public async Task ExpandAll_OpensEverySeriesAndGame()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var settings = new FakeSettingsRepository();
        var series = await seriesRepo.GetOrCreateByNameAsync("Book");
        await seriesRepo.SetExpandedAsync(series.Id, false);
        await settings.SetGameCollapsedAsync(GameTitle.Thief1, true);
        await repo.AddAsync(new FanMission { Title = "Part 1", Game = GameTitle.Thief2, FolderPath = "p1", SeriesId = series.Id, SeriesPosition = 1 });
        await repo.AddAsync(new FanMission { Title = "T1", Game = GameTitle.Thief1, FolderPath = "t1" });
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo, settingsRepo: settings);
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.ExpandAllCommand.ExecuteAsync(null);

        Assert.True(seriesRepo.SeriesList.Single().IsExpanded);
        Assert.False((await settings.GetAsync()).Thief1Collapsed);
        Assert.Equal(new[] { "T1", "Part 1" }, vm.VisibleMissions.Select(m => m.Title));
    }

    private static AvailableUpdate Update398 => new("3.9.8", "- Fixed things.");

    [Fact]
    public async Task CheckForUpdatesOnStartup_WithUpdate_SetsReadyState()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.True(vm.IsUpdateReady);
        Assert.Equal("3.9.8", vm.UpdateReadyVersion);
        Assert.Equal("- Fixed things.", vm.UpdateReleaseNotes);
    }

    [Fact]
    public async Task CheckForUpdatesOnStartup_WhenNotInstalled_DoesNothing()
    {
        var updates = new FakeUpdateService { IsInstalled = false, NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.Equal(0, updates.CheckCount);
        Assert.False(vm.IsUpdateReady);
        Assert.Null(vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdatesOnStartup_SwallowsFailures()
    {
        var updates = new FakeUpdateService { ThrowOnCheck = new HttpRequestException("offline") };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        await vm.CheckForUpdatesOnStartupAsync();

        Assert.False(vm.IsUpdateReady);
        Assert.Null(vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdates_WhenNotInstalled_ExplainsWhy()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { IsInstalled = false });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Updates are only available in the installed version.", vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task CheckForUpdates_WhenUpToDate_SaysSo()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService());

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal($"You're up to date (version {AppVersion.Current}).", vm.UpdateCheckMessage);
        Assert.False(vm.IsUpdateReady);
    }

    [Fact]
    public async Task CheckForUpdates_WithUpdate_ReportsReady()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { NextResult = Update398 });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Version 3.9.8 is ready — restart to update.", vm.UpdateCheckMessage);
        Assert.True(vm.IsUpdateReady);
    }

    [Fact]
    public async Task CheckForUpdates_OnFailure_ShowsTheError()
    {
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: new FakeUpdateService { ThrowOnCheck = new HttpRequestException("offline") });

        await vm.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't check for updates: offline", vm.UpdateCheckMessage);
    }

    [Fact]
    public async Task TryRestartToUpdate_WithReadyUpdate_Applies()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);
        await vm.CheckForUpdatesOnStartupAsync();

        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(1, updates.ApplyCount);
    }

    [Fact]
    public async Task TryRestartToUpdate_DuringRefresh_NeedsConfirmation()
    {
        var updates = new FakeUpdateService { NextResult = Update398 };
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);
        await vm.CheckForUpdatesOnStartupAsync();
        vm.IsThiefGuildRefreshRunning = true;

        Assert.False(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(0, updates.ApplyCount);
        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: true));
        Assert.Equal(1, updates.ApplyCount);
    }

    [Fact]
    public void TryRestartToUpdate_WithoutUpdate_DoesNothing()
    {
        var updates = new FakeUpdateService();
        var vm = MakeViewModel(new FakeMissionRepository(), updateService: updates);

        Assert.True(vm.TryRestartToUpdate(confirmedDespiteRefresh: false));
        Assert.Equal(0, updates.ApplyCount);
    }
}
