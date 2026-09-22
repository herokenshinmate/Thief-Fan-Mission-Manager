using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.Converters;

public class GameFilterOptionToIconSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            GameTitleNames.Thief1DisplayName => GameIconStore.GetIcon(GameTitle.Thief1),
            GameTitleNames.Thief2DisplayName => GameIconStore.GetIcon(GameTitle.Thief2),
            _ => null
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("GameFilterOptionToIconSourceConverter is one-way only.");
}
