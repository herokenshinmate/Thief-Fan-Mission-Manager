namespace ThiefManager.Models;

public static class MissionStatusNames
{
    public const string NotPlayedDisplayName = "Not Played";
    public const string InProgressDisplayName = "In Progress";
    public const string CompletedDisplayName = "Completed";
    public const string AbandonedDisplayName = "Abandoned";

    public static string ToDisplayName(this MissionStatus status) => status switch
    {
        MissionStatus.NotPlayed => NotPlayedDisplayName,
        MissionStatus.InProgress => InProgressDisplayName,
        MissionStatus.Completed => CompletedDisplayName,
        MissionStatus.Abandoned => AbandonedDisplayName,
        _ => status.ToString()
    };

    public static MissionStatus Parse(string displayName) => displayName switch
    {
        NotPlayedDisplayName => MissionStatus.NotPlayed,
        InProgressDisplayName => MissionStatus.InProgress,
        CompletedDisplayName => MissionStatus.Completed,
        AbandonedDisplayName => MissionStatus.Abandoned,
        _ => throw new ArgumentException($"Unknown status display name: {displayName}")
    };
}
