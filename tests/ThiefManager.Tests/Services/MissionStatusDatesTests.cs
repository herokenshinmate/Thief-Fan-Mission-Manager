using ThiefManager.Models;
using ThiefManager.Services;
using Xunit;

namespace ThiefManager.Tests.Services;

public class MissionStatusDatesTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0);

    [Fact]
    public void Apply_ToInProgress_SetsDateStartedWhenNotAlreadySet()
    {
        var mission = new FanMission();

        MissionStatusDates.Apply(mission, MissionStatus.InProgress, Now);

        Assert.Equal(Now, mission.DateStarted);
        Assert.Null(mission.DateCompleted);
    }

    [Fact]
    public void Apply_ToInProgress_PreservesExistingDateStarted()
    {
        var earlier = Now.AddDays(-3);
        var mission = new FanMission { DateStarted = earlier };

        MissionStatusDates.Apply(mission, MissionStatus.InProgress, Now);

        Assert.Equal(earlier, mission.DateStarted);
    }

    [Fact]
    public void Apply_ToCompleted_SetsBothDatesWhenNeverStarted()
    {
        var mission = new FanMission();

        MissionStatusDates.Apply(mission, MissionStatus.Completed, Now);

        Assert.Equal(Now, mission.DateStarted);
        Assert.Equal(Now, mission.DateCompleted);
    }

    [Fact]
    public void Apply_ToCompleted_PreservesExistingDateStartedAndSetsCompleted()
    {
        var earlier = Now.AddDays(-3);
        var mission = new FanMission { DateStarted = earlier };

        MissionStatusDates.Apply(mission, MissionStatus.Completed, Now);

        Assert.Equal(earlier, mission.DateStarted);
        Assert.Equal(Now, mission.DateCompleted);
    }

    [Fact]
    public void Apply_ToAbandoned_SetsDateStartedButNotCompleted()
    {
        var mission = new FanMission();

        MissionStatusDates.Apply(mission, MissionStatus.Abandoned, Now);

        Assert.Equal(Now, mission.DateStarted);
        Assert.Null(mission.DateCompleted);
    }

    [Fact]
    public void Apply_ToNotPlayed_ClearsBothDates()
    {
        var mission = new FanMission { DateStarted = Now.AddDays(-3), DateCompleted = Now.AddDays(-1) };

        MissionStatusDates.Apply(mission, MissionStatus.NotPlayed, Now);

        Assert.Null(mission.DateStarted);
        Assert.Null(mission.DateCompleted);
    }

    [Fact]
    public void Apply_BackToInProgressFromCompleted_ClearsDateCompletedButKeepsDateStarted()
    {
        var earlier = Now.AddDays(-3);
        var mission = new FanMission { DateStarted = earlier, DateCompleted = Now.AddDays(-1) };

        MissionStatusDates.Apply(mission, MissionStatus.InProgress, Now);

        Assert.Equal(earlier, mission.DateStarted);
        Assert.Null(mission.DateCompleted);
    }
}
