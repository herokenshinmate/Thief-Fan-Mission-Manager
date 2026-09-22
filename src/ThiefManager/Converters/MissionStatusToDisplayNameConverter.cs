using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class MissionStatusToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MissionStatus status ? status.ToDisplayName() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("MissionStatusToDisplayNameConverter is one-way only.");
}
