namespace ThiefManager.Models;

public static class ThiefGuildMetadata
{
    /// <summary>
    /// Stamped on a mission when its Thief Guild data was fetched by the current parser. The
    /// startup backfill re-fetches any linked mission below this, so raise it whenever the
    /// parser starts capturing a new field.
    /// </summary>
    public const int CurrentVersion = 2;
}
