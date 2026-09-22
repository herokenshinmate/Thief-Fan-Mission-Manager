using System.Globalization;
using System.Windows.Data;
using ThiefManager.Services;

namespace ThiefManager.Converters;

public class ExePathToIconSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string exePath ? ExeIconExtractor.ExtractIcon(exePath) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("ExePathToIconSourceConverter is one-way only.");
}
