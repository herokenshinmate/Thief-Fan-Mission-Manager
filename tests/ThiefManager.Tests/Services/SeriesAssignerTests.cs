using ThiefManager.Models;
using ThiefManager.Services;
using ThiefManager.Tests.Fakes;
using Xunit;

namespace ThiefManager.Tests.Services;

public class SeriesAssignerTests
{
    private readonly FakeSeriesRepository _seriesRepo = new(new FakeMissionRepository());

    [Fact]
    public async Task ApplyAsync_WithNoSeriesInfo_OnlyMarksChecked()
    {
        var mission = new FanMission();

        await SeriesAssigner.ApplyAsync(mission, null, _seriesRepo);

        Assert.True(mission.SeriesLookupChecked);
        Assert.Null(mission.SeriesId);
        Assert.Empty(_seriesRepo.SeriesList);
    }

    [Fact]
    public async Task ApplyAsync_WithSeriesInfo_CreatesSeriesAndAssigns()
    {
        var mission = new FanMission();

        await SeriesAssigner.ApplyAsync(mission, new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3), _seriesRepo);

        var series = Assert.Single(_seriesRepo.SeriesList);
        Assert.Equal(series.Id, mission.SeriesId);
        Assert.Equal(3, mission.SeriesPosition);
        Assert.True(mission.SeriesLookupChecked);
    }

    [Fact]
    public async Task ApplyAsync_SameThiefGuildSeriesTwice_ReusesSeries()
    {
        var part2 = new FanMission();
        var part3 = new FanMission();

        await SeriesAssigner.ApplyAsync(part2, new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 2), _seriesRepo);
        await SeriesAssigner.ApplyAsync(part3, new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3), _seriesRepo);

        Assert.Single(_seriesRepo.SeriesList);
        Assert.Equal(part2.SeriesId, part3.SeriesId);
    }

    [Fact]
    public async Task ApplyAsync_MissionAlreadyInSeries_KeepsItsSeries()
    {
        var mission = new FanMission { SeriesId = 42, SeriesPosition = 1 };

        await SeriesAssigner.ApplyAsync(mission, new ThiefGuildSeriesInfo(66445, "The Book of Prophecy", 3), _seriesRepo);

        Assert.Equal(42, mission.SeriesId);
        Assert.Equal(1, mission.SeriesPosition);
        Assert.True(mission.SeriesLookupChecked);
        Assert.Empty(_seriesRepo.SeriesList);
    }
}
