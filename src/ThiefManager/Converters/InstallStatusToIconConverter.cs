using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;
using Wpf.Ui.Controls;

namespace ThiefManager.Converters;

public class InstallStatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InstallStatus status ? status switch
        {
            InstallStatus.Installed => SymbolRegular.CheckmarkCircle24,
            InstallStatus.NotInstalled => SymbolRegular.ArrowDownload24,
            _ => SymbolRegular.CheckmarkCircle24
        } : SymbolRegular.CheckmarkCircle24;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("InstallStatusToIconConverter is one-way only.");
}
