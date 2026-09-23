using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace ThiefManager.Converters;

public class BoolToScanTypeIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? SymbolRegular.ArrowDownload24 : SymbolRegular.FolderAdd24;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("BoolToScanTypeIconConverter is one-way only.");
}
