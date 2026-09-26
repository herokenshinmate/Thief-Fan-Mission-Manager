using ThiefManager.Models;

namespace ThiefManager.ViewModels;

/// <summary>Raised by <see cref="MainViewModel.BriefingRequested"/> when Play should be interrupted
/// with a window instead of launching immediately - either to show the mission's briefing/notes,
/// warn about a NewDark version mismatch, or both.</summary>
public record MissionLaunchPrompt(FanMission Mission, string? VersionWarning);
