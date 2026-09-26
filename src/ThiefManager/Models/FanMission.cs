namespace ThiefManager.Models;

public class FanMission
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public GameTitle Game { get; set; }
    public string? Author { get; set; }
    public int? ReleaseYear { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.NotPlayed;
    public int? Rating { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public InstallStatus InstallStatus { get; set; } = InstallStatus.Installed;
    public string? ArchivePath { get; set; }
    public string? ThiefGuildUrl { get; set; }
    public bool ThiefGuildLookupDismissed { get; set; }
    public int? SeriesId { get; set; }
    public int? SeriesPosition { get; set; }
    public bool SeriesLookupChecked { get; set; }
    public double? ThiefGuildRating { get; set; }
    public int? ThiefGuildRatingCount { get; set; }
    public int? CampaignMissionCount { get; set; }
    public string? Description { get; set; }
    public string? SequelOfTitle { get; set; }
    public string? SequelOfUrl { get; set; }
    public string? HasSequelTitle { get; set; }
    public string? HasSequelUrl { get; set; }
    public int ThiefGuildMetadataVersion { get; set; }
    public string? RequiredNewDarkVersion { get; set; }
}
