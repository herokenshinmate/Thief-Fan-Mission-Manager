namespace ThiefManager;

public record ChangelogEntry(string Version, string Date, IReadOnlyList<string> Changes);

public static class ChangelogData
{
    /// <summary>Most recent version first.</summary>
    public static IReadOnlyList<ChangelogEntry> Entries { get; } = new List<ChangelogEntry>
    {
        new("3.9.9", "2026-09-26", new[]
        {
            "Fixed missions installed from Downloads with a long name failing to launch: install folder names are now kept to 30 characters, stripping spaces first and truncating if still too long.",
            "Fixed Thief Guild lookups missing FMs whose local name has no spaces/underscores between words or ends with a version tag (e.g. \"_v2\").",
            "Properties now show a Mission Briefing section (the mission's story synopsis from Thief Guild), and the author's Thief Guild notes (warnings, recommended settings, required NewDark version) fill in your personal Notes field when it's blank.",
            "Fixed the Mission Briefing sometimes coming back empty: Thief Guild's page data isn't always strict JSON, so a stray trailing comma could silently block the whole briefing from loading.",
            "Added a NewDark Version field to Settings for each game: auto-detected from the configured executable where possible (labeled as a guess), with a manual override you can type in yourself.",
            "Clicking Play now shows a bigger, resizable Mission Briefing window first (the story synopsis and any Thief Guild notes) when a mission has one, with a chance to cancel; turn this off in Settings.",
            "Settings is now organized into FM Folders, NewDark Versions, and General tabs.",
            "Added a General setting for whether double-clicking a mission plays it (the new default) or opens its Properties.",
            "Added a warning before playing a mission whose Thief Guild-listed required NewDark version is newer than the version configured for that game; turn this off in General settings."
        }),
        new("3.9.8", "2026-09-26", new[]
        {
            "No functional changes; this release verifies that automatic updates work end-to-end."
        }),
        new("3.9.7", "2026-09-25", new[]
        {
            "Thief FM Manager now has a proper installer and updates itself: new versions are downloaded in the background and a \"Restart to update\" link appears in the status bar when one is ready (see \"What's new\" for its changes).",
            "Added Check for Updates to the About window."
        }),
        new("3.9.6", "2026-09-25", new[]
        {
            "The Play button is now bright green and larger whenever a mission can be played.",
            "Added Collapse All (collapses every series, keeping the game banners open) and Expand All (opens every game and series) buttons.",
            "Rate a mission straight from its right-click menu (Rate > 0 to 5 stars, or Not Rated).",
            "Delete moved to the right-click menu as a red \"Delete from Library...\" item that asks for confirmation first, and now also deletes the mission's folder from disk when it's installed (the downloaded archive is kept).",
            "Add Mission moved to the menu bar, and the redundant Quick Scan Downloads button was removed (use Scan for New Missions)."
        }),
        new("3.9.5", "2026-09-25", new[]
        {
            "Removed the Game column from the mission list now that each game has its own banner; the Title column takes over its space. The list now sorts by Title by default, and Game is no longer offered as a sort option."
        }),
        new("3.9.4", "2026-09-25", new[]
        {
            "The mission list is now divided into a large banner per game (Thief 1, then Thief 2) showing how many missions you have, have completed and have installed; click a banner's arrow or double-click it to collapse that game, and the app remembers which games you collapsed.",
            "Campaigns stand out: missions that bundle several missions in one FM get a gold bar and a \"CAMPAIGN · N\" badge, and series get a blue bar with a \"SERIES\" badge on their header."
        }),
        new("3.9.3", "2026-09-25", new[]
        {
            "Added TG Rating and Type columns to the mission list, showing each mission's Thief Guild community rating (e.g. ★ 9.02 (229)) and whether it's a campaign; both are sortable.",
            "Mission Properties now show the Thief Guild rating, mission type and description, plus \"Sequel of\" / \"Has a sequel\" links for missions that aren't part of a series.",
            "Series now list the parts you don't own as dimmed placeholders (e.g. \"#1 · Dead Letter Box — not in library\"), with the header showing how many you own; double-click one to open its Thief Guild page.",
            "Added Refresh Thief Guild Data to re-fetch ratings and details for every linked mission. Missions already linked to Thief Guild are updated once automatically in the background."
        }),
        new("3.9.2", "2026-09-25", new[]
        {
            "Fixed saving a mission's Properties marking a Not Installed mission as Installed and forgetting its downloaded archive, which left it unable to be installed from the menu."
        }),
        new("3.9.1", "2026-09-25", new[]
        {
            "Missions that are part of a series (e.g. The Book of Prophecy Parts 1–3) are now grouped under a collapsible header in the mission list, in series order, showing how many you've completed.",
            "Thief Guild lookups now detect a mission's series and its position in it; missions already linked to Thief Guild are checked once in the background at startup.",
            "Set or change a mission's series by hand in Properties; right-click a series header to rename, collapse, or ungroup it."
        }),
        new("3.9.0", "2026-09-24", new[]
        {
            "The mission list now defaults to sorting by Game (Thief 1 first) instead of Title.",
            "Column headers in the mission list are now clickable to sort by that column; click again to reverse the direction. The active column shows a ▲/▼ arrow."
        }),
        new("3.8.0", "2026-09-24", new[]
        {
            "Added an Author column to the mission list (before Tags), and an Author filter in the toolbar that matches any credited co-author."
        }),
        new("3.7.2", "2026-09-24", new[]
        {
            "Fixed Thief Guild metadata lookups crediting a co-authored mission's \"Missions\" button as the author (e.g. \"Missions\" instead of the actual authors) instead of crediting every listed co-author."
        }),
        new("3.7.1", "2026-09-24", new[]
        {
            "Removed the File menu; Settings, Scan for New Missions, and Ignore List are now top-level menu items alongside Changelog, About, and Thief Guild."
        }),
        new("3.7.0", "2026-09-24", new[]
        {
            "Added an ignore list: right-click a Scan result and choose Ignore to hide it (and any similarly-named duplicates) from future scans.",
            "Added File > Ignore List... to view everything you've ignored and remove entries so they show up in scans again."
        }),
        new("3.6.5", "2026-09-24", new[]
        {
            "The Downloads scan now recognizes an archive as already installed even when its filename differs from the mission's name by casing, punctuation, a version tag (e.g. \"_v2\"), or a bracketed note (e.g. \"(fixed)\"), instead of only matching the exact archive file used to install it."
        }),
        new("3.6.4", "2026-09-24", new[]
        {
            "Removed the trailing \"...\" from the Changelog and About menu items."
        }),
        new("3.6.3", "2026-09-24", new[]
        {
            "The menu bar's top-level items (File/Changelog/About/Thief Guild) now show a gold outline and fill on hover, matching the app's accent color, instead of blending invisibly into the toolbar background."
        }),
        new("3.6.2", "2026-09-24", new[]
        {
            "Fixed the Play button opening FMSel's mission picker instead of loading the selected mission directly; it now passes -fm=<mission folder> to the game so it jumps straight in.",
            "Fixed the game process not being given a working directory, which could cause it to fail to start."
        }),
        new("3.6.1", "2026-09-24", new[]
        {
            "Removed the Help menu; Changelog and About are now top-level menu items."
        }),
        new("3.6.0", "2026-09-24", new[]
        {
            "Added a Tags column to the mission list, showing each tag as a chip; it fills the remaining table width without wrapping or requiring horizontal scrolling.",
            "Removed the empty space above the mission list that showed even when there was no error to display.",
            "Fixed the menu bar rendering much taller than its text."
        }),
        new("3.5.1", "2026-09-24", new[]
        {
            "Fixed Thief Guild metadata lookups picking up a screenshot uploader's name instead of the mission's actual credited author.",
            "Fixed the automatic Thief Guild lookup failing to find a mission when the search has exactly one match (Thief Guild redirects straight to the mission page instead of a results list)."
        }),
        new("3.5.0", "2026-09-24", new[]
        {
            "Added a Thief Guild menu item that opens thiefguild.com in your browser."
        }),
        new("3.4.1", "2026-09-24", new[]
        {
            "Widened the Properties dialog and reorganized its fields into a two-column layout that no longer needs scrolling.",
            "Fixed the Properties dialog leaving a large gap between the fields and the Save button.",
            "Fixed the Fetch Metadata button's text being clipped."
        }),
        new("3.4.0", "2026-09-24", new[]
        {
            "The Scan window now ignores folders that are already cataloged and any folder starting with a dot (e.g. \".fmsel.cache\").",
            "The Scan window now automatically scans both games' FM and Downloads folders on open, grouping results into collapsible, themed sections; replaced the manual scan buttons with a single Refresh button; and widened the window."
        }),
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
