using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.Converters;

public class GameTitleToIconSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GameTitle game ? GameIconStore.GetIcon(game) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("GameTitleToIconSourceConverter is one-way only.");
}
