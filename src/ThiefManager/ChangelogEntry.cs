namespace ThiefManager;

public record ChangelogEntry(string Version, string Date, IReadOnlyList<string> Changes);

public static class ChangelogData
{
    /// <summary>Most recent version first.</summary>
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new List<ChangelogEntry>
    {
        new("3.3.0", "2026-09-22", new[]
        {
            "Added a status bar at the bottom of the main window showing the app version.",
            "Added a Help menu with Changelog and About windows."
        }),
        new("3.2.1", "2026-09-22", new[]
        {
            "Fixed the Scan window's candidate list rendering blank until hovered."
        }),
        new("3.2.0", "2026-09-22", new[]
        {
            "Added Thief Guild metadata lookup: installing a mission automatically looks up its author, release year, and tags on thiefguild.com.",
            "Added a Thief Guild URL field and Fetch Metadata button to the Properties dialog for manual lookups."
        }),
        new("3.1.0", "2026-09-22", new[]
        {
            "Added a Quick Scan Downloads button that checks both games' download folders in one action.",
            "Fixed a bug where importing missions from multiple games in the same scan could tag them with the wrong game."
        }),
        new("3.0.1", "2026-09-22", new[]
        {
            "The toolbar now wraps onto additional rows instead of clipping buttons at small window widths."
        }),
        new("3.0.0", "2026-09-22", new[]
        {
            "Added the downloaded-FM install workflow: Install Status tracking, archive extraction (zip/7z/rar), and right-click Install/Uninstall.",
            "Added per-game Downloads folder settings and \"Install from Downloads\" scanning."
        }),
        new("2.3.2", "2026-09-22", new[]
        {
            "Fixed the mission grid's column headers turning bright white on hover.",
            "Status names now display with proper spacing (e.g. \"Not Played\" instead of \"NotPlayed\")."
        }),
        new("2.3.1", "2026-09-22", new[]
        {
            "Widened the main window so the toolbar buttons never clip.",
            "The Game filter dropdown now shows real per-game icons.",
            "Added icons next to the Game/Tag/Status/Sort filter labels."
        }),
        new("2.3.0", "2026-09-22", new[]
        {
            "Game icons are now extracted directly from your configured Thief executables instead of using a generic icon."
        }),
        new("2.2.1", "2026-09-22", new[]
        {
            "Title bars now use a distinct font, and the File menu bar padding was tightened."
        }),
        new("2.2.0", "2026-09-22", new[]
        {
            "Added colored icons throughout Settings, Scan, and the Properties dialog.",
            "Fixed the Properties dialog being cut off; added minimum window sizes app-wide.",
            "Gave every window a title bar icon, and styled the File menu bar so it reads as an actual toolbar."
        }),
        new("2.1.0", "2026-09-21", new[]
        {
            "Added Fluent icons to buttons and menu items throughout the app."
        }),
        new("2.0.1", "2026-09-21", new[]
        {
            "Fixed the mission grid rendering with a white background.",
            "Renamed the app to \"Thief FM Manager\" and added a custom app icon."
        }),
        new("2.0.0", "2026-09-21", new[]
        {
            "Adopted WPF-UI: Fluent Design windows, modern title bars, and Mica backdrop throughout the app."
        }),
        new("1.4.0", "2026-09-21", new[]
        {
            "Games now display their full titles (\"Thief: The Dark Project\" / \"Thief II: The Metal Age\") instead of raw internal names."
        }),
        new("1.3.0", "2026-09-21", new[]
        {
            "Date Started/Completed are now set automatically based on Status, and shown read-only.",
            "Rating is now a dropdown instead of free text."
        }),
        new("1.2.0", "2026-09-21", new[]
        {
            "Added a right-click context menu: Properties, quick Set Status, and Delete."
        }),
        new("1.1.2", "2026-09-21", new[]
        {
            "Fixed the File menu and mission list highlighting with unreadable light-on-light colors on hover."
        }),
        new("1.1.1", "2026-09-21", new[]
        {
            "Fixed the app closing unexpectedly after saving settings on first run.",
            "Fixed dark mode dropdown lists still showing a white background.",
            "Polished the Scan window's layout."
        }),
        new("1.1.0", "2026-09-21", new[]
        {
            "Added folder/executable browse buttons to Settings instead of typing paths by hand.",
            "Added a first-run setup prompt that opens Settings automatically.",
            "Added the first dark theme."
        }),
        new("1.0.0", "2026-09-21", new[]
        {
            "Initial release: a WPF desktop app for cataloging Thief 1/2 fan missions, with ratings, status tracking, folder scanning, and launching."
        })
    };
}
