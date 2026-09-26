namespace ThiefManager.Models;

public class AppSettings
{
    public int Id { get; set; }
    public string? Thief1FmFolder { get; set; }
    public string? Thief2FmFolder { get; set; }
    public string? Thief1ExePath { get; set; }
    public string? Thief2ExePath { get; set; }
    public string? Thief1DownloadsFolder { get; set; }
    public string? Thief2DownloadsFolder { get; set; }
    public bool Thief1Collapsed { get; set; }
    public bool Thief2Collapsed { get; set; }
    public string? Thief1NewDarkVersion { get; set; }
    public string? Thief2NewDarkVersion { get; set; }
    public bool ShowMissionBriefing { get; set; } = true;
    public bool DoubleClickLaunchesPlay { get; set; } = true;
    public bool WarnOnNewDarkVersionMismatch { get; set; } = true;
}
