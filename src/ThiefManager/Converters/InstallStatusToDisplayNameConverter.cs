using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class InstallStatusToDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus status ? status.ToDisplayName() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("InstallStatusToDisplayNameConverter is one-way only.");
}
