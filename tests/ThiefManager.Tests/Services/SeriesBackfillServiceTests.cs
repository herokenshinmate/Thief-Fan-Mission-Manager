using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using Xunit;

namespace ThiefManager.Tests.Services;

public class SeriesBackfillServiceTests
{
    private class UrlLookupStub : IThiefGuildLookupService
    {
        public Dictionary<string, ThiefGuildLookupResult?> Results { get; } = new();
        public List<string> FetchedUrls { get; } = new();
        public Action? OnFetch;

        public Task<ThiefGuildLookupResult?> SearchByTitleAsync(string title) => Task.FromResult<ThiefGuildLookupResult?>(null);

        public Task<ThiefGuildLookupResult?> FetchByUrlAsync(string url)
        {
            FetchedUrls.Add(url);
            OnFetch?.Invoke();
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

    public SeriesBackfillServiceTests() => _series = new FakeSeriesRepository(_missions);

    private SeriesBackfillService MakeService() =>
        new(_missions, _series, _lookup, (delay, _) => { _delays.Add(delay); return Task.CompletedTask; });

    private static ThiefGuildLookupResult Result(string url, ThiefGuildSeriesInfo? series) => new(null, null, "", url, series);

    [Fact]
    public async Task RunAsync_FetchesOnlyUncheckedMissionsWithUrlAndNoSeries()
    {
        await _missions.AddAsync(new FanMission { Title = "Candidate", ThiefGuildUrl = "u1" });
        await _missions.AddAsync(new FanMission { Title = "NoUrl" });
        await _missions.AddAsync(new FanMission { Title = "Checked", ThiefGuildUrl = "u3", SeriesLookupChecked = true });
        await _missions.AddAsync(new FanMission { Title = "HasSeries", ThiefGuildUrl = "u4", SeriesId = 5 });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.Equal(new[] { "u1" }, _lookup.FetchedUrls);
    }

    [Fact]
    public async Task RunAsync_AssignsSeriesMarksCheckedAndRaisesEvent()
    {
        await _missions.AddAsync(new FanMission { Title = "Part 3", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3));
        var service = MakeService();
        var raised = 0;
        service.SeriesAssigned += (_, _) => raised++;

        await service.RunAsync(null, CancellationToken.None);

        var mission = _missions.Missions.Single();
        Assert.Equal(_series.SeriesList.Single().Id, mission.SeriesId);
        Assert.Equal(3, mission.SeriesPosition);
        Assert.True(mission.SeriesLookupChecked);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task RunAsync_PageWithoutSeries_MarksCheckedWithoutEvent()
    {
        await _missions.AddAsync(new FanMission { Title = "Solo", ThiefGuildUrl = "u1" });
        _lookup.Results["u1"] = Result("u1", null);
        var service = MakeService();
        var raised = 0;
        service.SeriesAssigned += (_, _) => raised++;

        await service.RunAsync(null, CancellationToken.None);

        Assert.True(_missions.Missions.Single().SeriesLookupChecked);
        Assert.Null(_missions.Missions.Single().SeriesId);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task RunAsync_LookupFails_LeavesMissionUncheckedForNextLaunch()
    {
        await _missions.AddAsync(new FanMission { Title = "Offline", ThiefGuildUrl = "u1" });

        await MakeService().RunAsync(null, CancellationToken.None);

        Assert.False(_missions.Missions.Single().SeriesLookupChecked);
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
