using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class GameTitleToBrushConverter : IValueConverter
{
    private static readonly Brush Thief1Brush = new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27));
    private static readonly Brush Thief2Brush = new SolidColorBrush(Color.FromRgb(0x4D, 0xB6, 0xAC));

    static GameTitleToBrushConverter()
    {
        Thief1Brush.Freeze();
        Thief2Brush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GameTitle game ? game switch
        {
            GameTitle.Thief1 => Thief1Brush,
            GameTitle.Thief2 => Thief2Brush,
            _ => Thief1Brush
        } : Thief1Brush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("GameTitleToBrushConverter is one-way only.");
}
