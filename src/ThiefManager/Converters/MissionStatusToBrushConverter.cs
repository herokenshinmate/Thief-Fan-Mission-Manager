using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThiefManager.Models;

namespace ThiefManager.Converters;

public class MissionStatusToBrushConverter : IValueConverter
{
    private static readonly Brush NotPlayedBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
    private static readonly Brush InProgressBrush = new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6));
    private static readonly Brush CompletedBrush = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
    private static readonly Brush AbandonedBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x73, 0x73));

    static MissionStatusToBrushConverter()
    {
        NotPlayedBrush.Freeze();
        InProgressBrush.Freeze();
        CompletedBrush.Freeze();
        AbandonedBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is MissionStatus status ? status switch
        {
            MissionStatus.NotPlayed => NotPlayedBrush,
            MissionStatus.InProgress => InProgressBrush,
            MissionStatus.Completed => CompletedBrush,
            MissionStatus.Abandoned => AbandonedBrush,
            _ => NotPlayedBrush
        } : NotPlayedBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("MissionStatusToBrushConverter is one-way only.");
}
