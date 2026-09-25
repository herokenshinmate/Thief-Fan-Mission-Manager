using System.Windows;
using System.Windows.Controls;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

/// <summary>
/// Picks a GridView cell template by row type, so series headers and missions can share columns.
/// A column with no template for a row type shows an empty cell rather than the row's ToString().
/// </summary>
public class MissionListRowTemplateSelector : DataTemplateSelector
{
    private static readonly DataTemplate EmptyTemplate = CreateEmptyTemplate();

    public DataTemplate? MissionTemplate { get; set; }
    public DataTemplate? SeriesHeaderTemplate { get; set; }
    public DataTemplate? MissingPartTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container) => item switch
    {
        SeriesHeaderRow => SeriesHeaderTemplate ?? EmptyTemplate,
        MissionRow => MissionTemplate ?? EmptyTemplate,
        MissingPartRow => MissingPartTemplate ?? EmptyTemplate,
        _ => EmptyTemplate
    };

    private static DataTemplate CreateEmptyTemplate()
    {
        var template = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(Grid)) };
        template.Seal();
        return template;
    }
}
