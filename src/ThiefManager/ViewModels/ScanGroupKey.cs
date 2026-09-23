using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public record ScanGroupKey(GameTitle Game, bool IsDownload)
{
    public override string ToString() =>
        $"{Game.ToDisplayName()} — {(IsDownload ? "Downloaded, Not Installed" : "Installed, Not Cataloged")}";
}
