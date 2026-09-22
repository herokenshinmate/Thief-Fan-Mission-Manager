using System.Windows.Media.Imaging;
using ThiefManager.Models;

namespace ThiefManager.Services;

public static class GameIconStore
{
    private static BitmapSource? _thief1Icon;
    private static BitmapSource? _thief2Icon;

    public static void UpdatePaths(string? thief1ExePath, string? thief2ExePath)
    {
        _thief1Icon = ExeIconExtractor.ExtractIcon(thief1ExePath);
        _thief2Icon = ExeIconExtractor.ExtractIcon(thief2ExePath);
    }

    public static BitmapSource? GetIcon(GameTitle game) => game switch
    {
        GameTitle.Thief1 => _thief1Icon,
        GameTitle.Thief2 => _thief2Icon,
        _ => null
    };
}
