using ThiefManager.Data;
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
    private static MissionEditViewModel MakeViewModel(FakeMissionRepository repo, IThiefGuildLookupService? lookupService = null, FakeSeriesRepository? seriesRepo = null) =>
        new(repo, lookupService ?? new StubThiefGuildLookupService(null), seriesRepo ?? new FakeSeriesRepository(repo));

    [Fact]
    public async Task SaveCommand_OnNotInstalledMission_KeepsInstallStatusAndArchivePath()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission
        {
            Title = "Archived",
            FolderPath = "fms/archived",
            InstallStatus = InstallStatus.NotInstalled,
            ArchivePath = @"C:\Downloads\archived.zip"
        });
        var vm = MakeViewModel(repo);
        vm.LoadFrom(repo.Missions.Single());
        vm.Notes = "edited";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = repo.Missions.Single();
        Assert.Equal(InstallStatus.NotInstalled, saved.InstallStatus);
        Assert.Equal(@"C:\Downloads\archived.zip", saved.ArchivePath);
    }

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

    private static async Task<(FakeMissionRepository Repo, FakeSeriesRepository SeriesRepo, FanMission Mission)> MissionInSeriesAsync()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var series = await seriesRepo.GetOrCreateByNameAsync("The Book of Prophecy");
        await repo.AddAsync(new FanMission { Title = "Part 3", FolderPath = "p3", SeriesId = series.Id, SeriesPosition = 3 });
        return (repo, seriesRepo, repo.Missions.Single());
    }

    [Fact]
    public async Task LoadSeriesOptionsAsync_ListsNamesAndShowsCurrentSeries()
    {
        var (repo, seriesRepo, mission) = await MissionInSeriesAsync();
        await seriesRepo.GetOrCreateByNameAsync("Another Saga");
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.LoadFrom(mission);

        await vm.LoadSeriesOptionsAsync();

        Assert.Equal(new[] { "Another Saga", "The Book of Prophecy" }, vm.SeriesNameOptions);
        Assert.Equal("The Book of Prophecy", vm.SeriesName);
        Assert.Equal(3, vm.SeriesPosition);
    }

    [Fact]
    public async Task SaveCommand_WithNewSeriesName_CreatesSeriesAndAssigns()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadSeriesOptionsAsync();
        vm.Title = "Keeper 1";
        vm.SeriesName = " Keeper Chronicles ";
        vm.SeriesPosition = 1;

        await vm.SaveCommand.ExecuteAsync(null);

        var series = Assert.Single(seriesRepo.SeriesList);
        Assert.Equal("Keeper Chronicles", series.Name);
        Assert.Equal(series.Id, repo.Missions.Single().SeriesId);
        Assert.Equal(1, repo.Missions.Single().SeriesPosition);
        Assert.True(repo.Missions.Single().SeriesLookupChecked);
    }

    [Fact]
    public async Task SaveCommand_WithExistingNameInDifferentCase_JoinsThatSeries()
    {
        var (repo, seriesRepo, _) = await MissionInSeriesAsync();
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        await vm.LoadSeriesOptionsAsync();
        vm.Title = "Part 2";
        vm.SeriesName = "the book of prophecy";
        vm.SeriesPosition = 2;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(seriesRepo.SeriesList);
        Assert.All(repo.Missions, m => Assert.Equal(seriesRepo.SeriesList[0].Id, m.SeriesId));
    }

    [Fact]
    public async Task SaveCommand_WithBlankSeriesName_ClearsSeriesAndRemovesOrphan()
    {
        var (repo, seriesRepo, mission) = await MissionInSeriesAsync();
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.LoadFrom(mission);
        await vm.LoadSeriesOptionsAsync();
        vm.SeriesName = "  ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(repo.Missions.Single().SeriesId);
        Assert.Null(repo.Missions.Single().SeriesPosition);
        Assert.Empty(seriesRepo.SeriesList);
    }

    [Fact]
    public async Task SaveCommand_WithoutTouchingSeries_KeepsSeries()
    {
        var (repo, seriesRepo, mission) = await MissionInSeriesAsync();
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.LoadFrom(mission);
        await vm.LoadSeriesOptionsAsync();
        vm.Notes = "great";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(seriesRepo.SeriesList.Single().Id, repo.Missions.Single().SeriesId);
        Assert.Equal(3, repo.Missions.Single().SeriesPosition);
        Assert.False(repo.Missions.Single().SeriesLookupChecked);
    }

    [Fact]
    public async Task SaveCommand_WithoutLoadingSeriesOptions_KeepsSeries()
    {
        var (repo, seriesRepo, mission) = await MissionInSeriesAsync();
        var vm = MakeViewModel(repo, seriesRepo: seriesRepo);
        vm.LoadFrom(mission);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(seriesRepo.SeriesList.Single().Id, repo.Missions.Single().SeriesId);
        Assert.Equal(3, repo.Missions.Single().SeriesPosition);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_WithSeries_PrefillsBlankSeriesAndLinksThiefGuildId()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(null, null, "", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3)));
        var vm = MakeViewModel(repo, lookup, seriesRepo);
        await vm.LoadSeriesOptionsAsync();
        vm.Title = "Part 3";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("The Book of Prophecy", vm.SeriesName);
        var series = Assert.Single(seriesRepo.SeriesList);
        Assert.Equal(66445, series.ThiefGuildSeriesId);
        Assert.Equal(3, repo.Missions.Single().SeriesPosition);
        Assert.True(repo.Missions.Single().SeriesLookupChecked);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_DoesNotOverwriteSeriesAlreadyEntered()
    {
        var (repo, seriesRepo, mission) = await MissionInSeriesAsync();
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(null, null, "", "u",
            new ThiefGuildSeriesInfo(1, "Something Else", 9)));
        var vm = MakeViewModel(repo, lookup, seriesRepo);
        vm.LoadFrom(mission);
        await vm.LoadSeriesOptionsAsync();

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);

        Assert.Equal("The Book of Prophecy", vm.SeriesName);
        Assert.Equal(3, vm.SeriesPosition);
    }

    private static FanMission MissionWithThiefGuildData() => new()
    {
        Title = "Endless Rain",
        FolderPath = "er",
        ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/2535/endless-rain",
        ThiefGuildRating = 9.02,
        ThiefGuildRatingCount = 229,
        CampaignMissionCount = 1,
        Description = "A rainy night.",
        SequelOfTitle = "Between These Dark Walls",
        SequelOfUrl = "https://www.thiefguild.com/works/a",
        HasSequelTitle = "The Chalice of Souls",
        HasSequelUrl = "https://www.thiefguild.com/works/b",
        ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion
    };

    [Fact]
    public async Task SaveCommand_RoundTripsThiefGuildColumns()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(MissionWithThiefGuildData());
        var vm = MakeViewModel(repo);
        vm.LoadFrom(repo.Missions.Single());
        vm.Notes = "edited";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = repo.Missions.Single();
        Assert.Equal(9.02, saved.ThiefGuildRating);
        Assert.Equal(229, saved.ThiefGuildRatingCount);
        Assert.Equal(1, saved.CampaignMissionCount);
        Assert.Equal("A rainy night.", saved.Description);
        Assert.Equal("Between These Dark Walls", saved.SequelOfTitle);
        Assert.Equal("https://www.thiefguild.com/works/a", saved.SequelOfUrl);
        Assert.Equal("The Chalice of Souls", saved.HasSequelTitle);
        Assert.Equal("https://www.thiefguild.com/works/b", saved.HasSequelUrl);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, saved.ThiefGuildMetadataVersion);
    }

    [Fact]
    public void LoadFrom_FillsThiefGuildSection()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.LoadFrom(MissionWithThiefGuildData());

        Assert.Equal("★ 9.02 from 229 ratings · Single mission", vm.ThiefGuildSummary);
        Assert.Equal("A rainy night.", vm.Description);
        Assert.Equal(new ThiefGuildLink("Between These Dark Walls", "https://www.thiefguild.com/works/a"), vm.SequelOf);
        Assert.True(vm.ShowSequelLinks);
        Assert.True(vm.HasThiefGuildInfo);
    }

    [Fact]
    public void ShowSequelLinks_IsFalseForSeriesMembers()
    {
        var vm = MakeViewModel(new FakeMissionRepository());
        vm.LoadFrom(MissionWithThiefGuildData());

        vm.SeriesName = "Some Series";

        Assert.False(vm.ShowSequelLinks);
    }

    [Fact]
    public void LoadFrom_WithoutThiefGuildData_HidesTheSection()
    {
        var vm = MakeViewModel(new FakeMissionRepository());

        vm.LoadFrom(new FanMission { Title = "Plain", FolderPath = "p" });

        Assert.Null(vm.ThiefGuildSummary);
        Assert.False(vm.HasThiefGuildInfo);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_FillsSectionAndSaveStampsVersion()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(new FanMission { Title = "The Black Parade", FolderPath = "bp" });
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(
            null, null, "", "https://www.thiefguild.com/fanmissions/1/the-black-parade",
            Rating: 9.7, RatingCount: 331, CampaignMissionCount: 10, Description: "A campaign."));
        var vm = MakeViewModel(repo, lookup);
        vm.LoadFrom(repo.Missions.Single());

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("★ 9.70 from 331 ratings · Campaign of 10 missions", vm.ThiefGuildSummary);
        var saved = repo.Missions.Single();
        Assert.Equal(10, saved.CampaignMissionCount);
        Assert.Equal("A campaign.", saved.Description);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, saved.ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task FetchThiefGuildMetadata_ThenSave_StoresSeriesParts()
    {
        var repo = new FakeMissionRepository();
        var seriesRepo = new FakeSeriesRepository(repo);
        var lookup = new StubThiefGuildLookupService(new ThiefGuildLookupResult(
            null, null, "", "https://www.thiefguild.com/fanmissions/66450/x",
            new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3, new[]
            {
                new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
                new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
            })));
        var vm = MakeViewModel(repo, lookup, seriesRepo);
        await vm.LoadSeriesOptionsAsync();
        vm.Title = "In the Lion's Den";

        await vm.FetchThiefGuildMetadataCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Dead Letter Box", "In the Lion's Den" }, seriesRepo.PartsList.OrderBy(p => p.Position).Select(p => p.Title));
    }

    [Fact]
    public async Task SaveCommand_AfterChangingUrlWithoutFetch_ClearsThiefGuildDataAndResetsVersion()
    {
        var repo = new FakeMissionRepository();
        await repo.AddAsync(MissionWithThiefGuildData());
        var vm = MakeViewModel(repo);
        vm.LoadFrom(repo.Missions.Single());

        vm.ThiefGuildUrl = "https://www.thiefguild.com/fanmissions/9999/different-mission";
        await vm.SaveCommand.ExecuteAsync(null);

        var saved = repo.Missions.Single();
        Assert.Equal("https://www.thiefguild.com/fanmissions/9999/different-mission", saved.ThiefGuildUrl);
        Assert.Null(saved.ThiefGuildRating);
        Assert.Null(saved.ThiefGuildRatingCount);
        Assert.Null(saved.CampaignMissionCount);
        Assert.Null(saved.Description);
        Assert.Null(saved.SequelOfTitle);
        Assert.Null(saved.SequelOfUrl);
        Assert.Null(saved.HasSequelTitle);
        Assert.Null(saved.HasSequelUrl);
        Assert.Equal(0, saved.ThiefGuildMetadataVersion);
    }
}
