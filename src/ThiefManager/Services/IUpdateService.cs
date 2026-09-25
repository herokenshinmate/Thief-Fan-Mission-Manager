namespace ThiefManager.Services;

public record AvailableUpdate(string Version, string? ReleaseNotesMarkdown);

public interface IUpdateService
{
    /// <summary>False when running outside a Velopack install (e.g. a dev build); all update features are then inert.</summary>
    bool IsInstalled { get; }

    /// <summary>Checks GitHub for a newer release and downloads it. Returns it once ready to apply, or null if up to date.</summary>
    Task<AvailableUpdate?> CheckAndDownloadAsync();

    /// <summary>Applies the downloaded update and restarts the app.</summary>
    void ApplyAndRestart();
}
