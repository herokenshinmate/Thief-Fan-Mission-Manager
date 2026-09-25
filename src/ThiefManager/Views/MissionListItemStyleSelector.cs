using System.Windows;
using System.Windows.Controls;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

/// <summary>Game banners get a full-width container; every other row keeps the grid row style.</summary>
public class MissionListItemStyleSelector : StyleSelector
{
    public Style? RowStyle { get; set; }
    public Style? BannerStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container) =>
        item is GameHeaderRow ? BannerStyle : RowStyle;
}
