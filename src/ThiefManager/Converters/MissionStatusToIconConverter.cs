using System.Globalization;
using System.Windows.Data;
using ThiefManager.Models;
using Wpf.Ui.Controls;

namespace ThiefManager.Converters;

public class MissionStatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MissionStatus status ? status switch
        {
            MissionStatus.NotPlayed => SymbolRegular.Circle24,
            MissionStatus.InProgress => SymbolRegular.PlayCircle24,
            MissionStatus.Completed => SymbolRegular.CheckmarkCircle24,
            MissionStatus.Abandoned => SymbolRegular.DismissCircle24,
            _ => SymbolRegular.Circle24
        } : SymbolRegular.Circle24;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("MissionStatusToIconConverter is one-way only.");
}
