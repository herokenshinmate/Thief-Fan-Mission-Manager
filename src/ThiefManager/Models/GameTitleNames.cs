namespace ThiefManager.Models;

public static class GameTitleNames
{
    public const string Thief1DisplayName = "Thief: The Dark Project";
    public const string Thief2DisplayName = "Thief II: The Metal Age";

    public static string ToDisplayName(this GameTitle game) => game switch
    {
        GameTitle.Thief1 => Thief1DisplayName,
        GameTitle.Thief2 => Thief2DisplayName,
        _ => game.ToString()
    };

    public static GameTitle Parse(string displayName) => displayName switch
    {
        Thief1DisplayName => GameTitle.Thief1,
        Thief2DisplayName => GameTitle.Thief2,
        _ => throw new ArgumentException($"Unknown game display name: {displayName}")
    };
}
