namespace ThiefManager.Models;

public static class InstallStatusNames
{
    public const string InstalledDisplayName = "Installed";
    public const string NotInstalledDisplayName = "Not Installed";

    public static string ToDisplayName(this InstallStatus status) => status switch
    {
        InstallStatus.Installed => InstalledDisplayName,
        InstallStatus.NotInstalled => NotInstalledDisplayName,
        _ => status.ToString()
    };

    public static InstallStatus Parse(string displayName) => displayName switch
    {
        InstalledDisplayName => InstallStatus.Installed,
        NotInstalledDisplayName => InstallStatus.NotInstalled,
        _ => throw new ArgumentException($"Unknown install status display name: {displayName}")
    };
}
