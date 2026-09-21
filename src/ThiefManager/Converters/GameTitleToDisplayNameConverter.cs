using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class GameTitleToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GameTitle game ? game.ToDisplayName() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("GameTitleToDisplayNameConverter is one-way only.");
}
