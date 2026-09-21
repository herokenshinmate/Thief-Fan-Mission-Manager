using ThiefManager.Models;
using Xunit;

namespace ThiefManager.Tests.Models;

public class FanMissionTests
{
    [Fact]
    public void NewFanMission_DefaultsToNotPlayedWithEmptyTags()
    {
        var mission = new FanMission();

        Assert.Equal(MissionStatus.NotPlayed, mission.Status);
        Assert.Equal(string.Empty, mission.Tags);
        Assert.Equal(string.Empty, mission.Title);
        Assert.Equal(string.Empty, mission.FolderPath);
    }
}
