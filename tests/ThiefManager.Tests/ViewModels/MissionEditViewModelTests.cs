using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class StubThiefGuildLookupService : IThiefGuildLookupService
{
    private readonly ThiefGuildLookupResult? _result;
    public string? LastSearchedTitle;
    public string? LastFetchedUrl;

    public StubThiefGuildLookupService(ThiefGuildLookupResult? result) => _result = result;

    public Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title)
    {
        LastSearchedTitle = title;
        return Task.FromResult(_result);
    }

    public Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url)
    {
        LastFetchedUrl = url;
        return Task.FromResult(_result);
    }
}

public class MissionEditViewModelTests
{
    private static MissionEditViewModel MakeViewModel(FakeMissionRepository repo, IThiefGuildLookupService? lookupService = null) =>
        new(repo, lookupService ?? new StubThiefGuildLookupService(null));

    [Fact]
    public async Task SaveCommand_WithNoLoadedMission_AddsNewMissionToRepository()
    {
        var repo = new FakeMissionRepository();
        var vm = MakeViewModel(repo);
        vm.Title = "New Mission";
        vm.Game = GameTitle.Thief2;
        vm.FolderPath = @"C:\fms\NewMission";

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
        var vm = MakeViewModel(repo);
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
    public async Task SaveCommand_SettingStatusToCompleted_AutoPopulatesBothDates()
    {
        var repo = new FakeMissionRepository();
        var vm = MakeViewModel(repo);
        vm.Title = "New Mission";
        vm.FolderPath = "p1";
        vm.Status = MissionStatus.Completed;

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.NotNull(saved.DateStarted);
        Assert.NotNull(saved.DateCompleted);
        Assert.Equal(saved.DateStarted, vm.DateStarted);
        Assert.Equal(saved.DateCompleted, vm.DateCompleted);
    }

    [Fact]
    public async Task SaveCommand_SettingStatusBackToNotPlayed_ClearsBothDates()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission
        {
            Title = "Original",
            FolderPath = "p1",
            Status = MissionStatus.Completed,
            DateStarted = DateTime.Now.AddDays(-5),
            DateCompleted = DateTime.Now.AddDays(-1)
        });
        var existing = repo.Missions.Single();
        var vm = MakeViewModel(repo);
        vm.LoadFrom(existing);

        vm.Status = MissionStatus.NotPlayed;
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Null(saved.DateStarted);
        Assert.Null(saved.DateCompleted);
    }

    [Fact]
    public async Task SaveCommand_RaisesSavedEvent()
    {
        var repo = new FakeMissionRepository();
        var vm = MakeViewModel(repo);
        vm.Title = "X";
        vm.FolderPath = "p";
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
    }

    [Fact]
    public async Task SaveCommand_PreservesThiefGuildUrlAndDismissedFlag()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission
        {
            Title = "Original",
            FolderPath = "p1",
            ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/1/original",
            ThiefGuildLookupDismissed = true
        });
        var existing = repo.Missions.Single();
        var vm = MakeViewModel(repo);
        vm.LoadFrom(existing);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(repo.Missions);
        Assert.Equal("https://www.thiefguild.com/fanmissions/1/original", saved.ThiefGuildUrl);
        Assert.True(saved.ThiefGuildLookupDismissed);
    }

    [Fact]
    public async Task FetchThiefGuildMetadataCommand_WithNoUrl_SearchesByTitleAndFillsBlankFields()
    {
        var repo = new FakeMissionRepository();
        var lookupService = new StubThiefGuildLookupService(
            new ThiefGuildLookupResult("Some Author", 2020, "Church, City", "https://www.thiefguild.com/fanmissions/1/mission"));
        var vm = MakeViewModel(repo, lookupService);
        vm.Title = "Mission";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);

        Assert.Equal("Mission", lookupService.LastSearchedTitle);
        Assert.Equal("Some Author", vm.Author);
        Assert.Equal(2020, vm.ReleaseYear);
        Assert.Equal("Church, City", vm.Tags);
        Assert.Equal("https://www.thiefguild.com/fanmissions/1/mission", vm.ThiefGuildUrl);
    }

    [Fact]
    public async Task FetchThiefGuildMetadataCommand_WithUrlEntered_FetchesByUrlInstead()
    {
        var repo = new FakeMissionRepository();
        var lookupService = new StubThiefGuildLookupService(
            new ThiefGuildLookupResult("Some Author", 2020, "Church", "https://www.thiefguild.com/fanmissions/1/mission"));
        var vm = MakeViewModel(repo, lookupService);
        vm.Title = "Mission";
        vm.ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/1/mission";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);

        Assert.Equal("https://www.thiefguild.com/fanmissions/1/mission", lookupService.LastFetchedUrl);
        Assert.Null(lookupService.LastSearchedTitle);
    }

    [Fact]
    public async Task FetchThiefGuildMetadataCommand_WhenNotFound_SetsStatusMessageWithoutChangingFields()
    {
        var repo = new FakeMissionRepository();
        var vm = MakeViewModel(repo, new StubThiefGuildLookupService(null));
        vm.Title = "Mission";
        vm.Author = "Existing Author";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);

        Assert.Equal("Existing Author", vm.Author);
        Assert.False(string.IsNullOrEmpty(vm.ThiefGuildLookupStatus));
    }

    [Fact]
    public async Task FetchThiefGuildMetadataCommand_ExplicitFetchOverwritesExistingFields()
    {
        var repo = new FakeMissionRepository();
        var lookupService = new StubThiefGuildLookupService(
            new ThiefGuildLookupResult("New Author", 2021, "Escape", "https://www.thiefguild.com/fanmissions/2/mission"));
        var vm = MakeViewModel(repo, lookupService);
        vm.Title = "Mission";
        vm.Author = "Old Author";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);

        Assert.Equal("New Author", vm.Author);
    }
}
