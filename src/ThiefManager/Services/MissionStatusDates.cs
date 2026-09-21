using ThiefManager.Models;

namespace ThiefManager.Services;

public static class MissionStatusDates
{
    public static void Apply(FanMission mission, MissionStatus newStatus, DateTime now)
    {
        mission.Status = newStatus;

        switch (newStatus)
        {
            case MissionStatus.NotPlayed:
                mission.DateStarted = null;
                mission.DateCompleted = null;
                break;
            case MissionStatus.InProgress:
                mission.DateStarted ??= now;
                mission.DateCompleted = null;
                break;
            case MissionStatus.Completed:
                mission.DateStarted ??= now;
                mission.DateCompleted ??= now;
                break;
            case MissionStatus.Abandoned:
                mission.DateStarted ??= now;
                mission.DateCompleted = null;
                break;
        }
    }
}
