using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using Xunit;

namespace ThiefManager.Tests.Services;

public class ThiefGuildBackfillServiceTests
{
    private class UrlLookupStub : IThiefGuildLookupService
    {
        public Dictionary<string, ThiefGuildLookupResult?> Results { get; } = new();
        public List<string> FetchedUrls { get; } = new();
        public HashSet<string> Throws { get; } = new();
        public Action? OnFetch;

        public Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title) => Task.FromResult<ThiefGuildLookupResult?>(null);

        public Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url)
        {
            FetchedUrls.Add(url);
            OnFetch?.Invoke();
            if (Throws.Contains(url))
                throw new InvalidOperationException($"Bad URL: {url}");
            return Task.FromResult(Results.GetValueOrDefault(url));
        }
    }

    private class RecordingProgress : IProgress<(int Done, int Total)>
    {
        public List<(int Done, int Total)> Reports { get; } = new();
        public void Report((int Done, int Total) value) => Reports.Add(value);
    }

    private readonly FakeMissionRepository _missions = new();
    private readonly FakeSeriesRepository _series;
    private readonly UrlLookupStub _lookup = new();
    private readonly List<TimeSpan> _delays = new();

    public ThiefGuildBackfillServiceTests() => _series = new FakeSeriesRepository(_missions);

    private ThiefGuildBackfillService MakeService() =>
        new(_missions, _series, _lookup, (delay, _) => { _delays.Add(delay); return Task.CompletedTask; });

    private static ThiefGuildLookupResult Result(string url, ThiefGuildSeriesInfo? series = null, double? rating = 8.0) =>
        new(null, null, "", url, series, Rating: rating, RatingCount: rating is null ? null : 10, CampaignMissionCount: 1);

    private static ThiefGuildSeriesInfo BookSeries(int position) => new(66445, "The Book of Prophecy", position, new[]
    {
        new ThiefGuildSeriesPartInfo(1, "Dead Letter Box", "https://www.thiefguild.com/fanmissions/2684/p1"),
        new ThiefGuildSeriesPartInfo(2, "The Hidden City", "https://www.thiefguild.com/fanmissions/2682/p2"),
        new ThiefGuildSeriesPartInfo(3, "In the Lion's Den", null)
    });

    [Fact]
    public async Task RunAsync_FetchesOnlyLinkedMissionsBelowCurrentVersion()
    {
        await _missions.AddAsync(new FanMission { Title = "Old", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "NoUrl" });
        await _missions.AddAsync(new FanMission { Title = "Current", ThiefGuildUrl = "u3", ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion });
        await _missions.AddAsync(new FanMission { Title = "OldInSeries", ThiefGuildUrl = "u4", SeriesId = 5, SeriesLookupChecked = true });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(new[] { "u1", "u4" }, _lookup.FetchedUrls);
    }

    [Fact]
    public async Task RunAsync_RefreshAll_FetchesEveryLinkedMission()
    {
        await _missions.AddAsync(new FanMission { Title = "Old", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "Current", ThiefGuildUrl = "u2", ThiefGuildMetadataVersion = ThiefGuildMetadata.CurrentVersion });
        await _missions.AddAsync(new FanMission { Title = "NoUrl" });

        await MakeService().RunAsync(null, CancellationToken.None, refreshAll: true);

        Assert.Equal(new[] { "u1", "u2" }, _lookup.FetchedUrls);
    }

    [Fact]
    public async Task RunAsync_Success_StoresMetadataStampsVersionAndRaisesMissionUpdated()
    {
        await _missions.AddAsync(new FanMission { Title = "M", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", rating: 9.02);
        var service = MakeService();
        var raised = 0;
        service.MissionUpdated += (_, _) => raised++;

        await service.RunAsync(null, CancellationToken.None);

        var mission = _missions.Missions.Single();
        Assert.Equal(9.02, mission.ThiefGuildRating);
        Assert.Equal(1, mission.CampaignMissionCount);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, mission.ThiefGuildMetadataVersion);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task RunAsync_AssignsSeriesAndStoresParts()
    {
        await _missions.AddAsync(new FanMission { Title = "Part 3", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", BookSeries(3));

        await MakeService().RunAsync(null, CancellationToken.None);

        var mission = _missions.Missions.Single();
        Assert.Equal(_series.SeriesList.Single().Id, mission.SeriesId);
        Assert.Equal(3, mission.SeriesPosition);
        Assert.Equal(3, _series.PartsList.Count);
    }

    [Fact]
    public async Task RunAsync_DoesNotRegroupAnUngroupedMission()
    {
        await _missions.AddAsync(new FanMission { Title = "Part 3", ThiefGuildUrl = "u1", SeriesLookupChecked = true });
        _lookup.Results["u1"] = Result("u1", BookSeries(3));

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Null(_missions.Missions.Single().SeriesId);
        Assert.Empty(_series.SeriesList);
    }

    [Fact]
    public async Task RunAsync_ResultWithoutSeries_KeepsStoredParts()
    {
        var series = await _series.GetOrCreateByThiefGuildIdAsync(66445, "The Book of Prophecy");
        await _series.ReplacePartsAsync(series.Id, new[] { new SeriesPart { Position = 1, Title = "Kept" } });
        await _missions.AddAsync(new FanMission { Title = "Part 1", ThiefGuildUrl = "u1", SeriesId = series.Id, SeriesPosition = 1, SeriesLookupChecked = true });
        _lookup.Results["u1"] = Result("u1", series: null);

        await MakeService().RunAsync(null, CancellationToken.None, refreshAll: true);

        Assert.Equal(series.Id, _missions.Missions.Single().SeriesId);
        Assert.Equal("Kept", _series.PartsList.Single().Title);
    }

    [Fact]
    public async Task RunAsync_LookupFails_LeavesVersionForNextLaunch()
    {
        await _missions.AddAsync(new FanMission { Title = "Offline", ThiefGuildUrl = "u1" });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(0, _missions.Missions.Single().ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task RunAsync_LookupThrows_SkipsMissionAndContinues()
    {
        await _missions.AddAsync(new FanMission { Title = "Broken", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "Fine", ThiefGuildUrl = "u2" });
        _lookup.Throws.Add("u1");
        _lookup.Results["u2"] = Result("u2");

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(0, _missions.Missions.Single(m => m.Title == "Broken").ThiefGuildMetadataVersion);
        Assert.Equal(ThiefGuildMetadata.CurrentVersion, _missions.Missions.Single(m => m.Title == "Fine").ThiefGuildMetadataVersion);
    }

    [Fact]
    public async Task RunAsync_WaitsOneSecondBetweenRequests_AndReportsProgress()
    {
        await _missions.AddAsync(new FanMission { Title = "A", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "B", ThiefGuildUrl = "u2" });
        await _missions.AddAsync(new FanMission { Title = "C", ThiefGuildUrl = "u3" });
        var progress = new RecordingProgress();

        await MakeService().RunAsync(progress, CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1) }, _delays);
        Assert.Equal(new[] { (0, 3), (1, 3), (2, 3), (3, 3) }, progress.Reports);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsFetching()
    {
        await _missions.AddAsync(new FanMission { Title = "A", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "B", ThiefGuildUrl = "u2" });
        using var cts = new CancellationTokenSource();
        _lookup.OnFetch = cts.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MakeService().RunAsync(null, cts.Token));

        Assert.Equal(new[] { "u1" }, _lookup.FetchedUrls);
    }
}
