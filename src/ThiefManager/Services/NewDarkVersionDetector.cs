using System.Diagnostics;
using System.IO;

namespace ThiefManager.Services;

/// <summary>
/// Best-effort local NewDark detection: there's no reliable API for this, so it just reads the
/// Win32 version resource off the configured game/loader executable. NewDark stamps the "Product
/// Version" field with its own version (e.g. "1.28") rather than the file version, which stays a
/// generic "1.0.0.0" - confirmed by checking a real NewDark-patched Thief2.exe. Some setups point
/// at a loader rather than the game itself, so the result is a guess to show the user, not a fact
/// to rely on.
/// </summary>
public static class NewDarkVersionDetector
{
    public static string? TryDetect(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return null;

        try
        {
            var version = FileVersionInfo.GetVersionInfo(exePath).ProductVersion?.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
