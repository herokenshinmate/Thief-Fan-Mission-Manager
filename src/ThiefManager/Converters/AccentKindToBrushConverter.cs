using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ThiefManager.ViewModels;

namespace ThiefManager.Converters;

public class AccentKindToBrushConverter : IValueConverter
{
    public Brush? CampaignBrush { get; set; }
    public Brush? SeriesBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        AccentKind.Campaign => CampaignBrush ?? Brushes.Transparent,
        AccentKind.Series => SeriesBrush ?? Brushes.Transparent,
        _ => Brushes.Transparent
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("AccentKindToBrushConverter is one-way only.");
}
