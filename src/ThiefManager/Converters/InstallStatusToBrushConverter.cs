using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class InstallStatusToBrushConverter : IValueConverter
{
    private static readonly Brush InstalledBrush = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
    private static readonly Brush NotInstalledBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));

    static InstallStatusToBrushConverter()
    {
        InstalledBrush.Freeze();
        NotInstalledBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus status ? status switch
        {
            InstallStatus.Installed => InstalledBrush,
            InstallStatus.NotInstalled => NotInstalledBrush,
            _ => InstalledBrush
        } : InstalledBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("InstallStatusToBrushConverter is one-way only.");
}
