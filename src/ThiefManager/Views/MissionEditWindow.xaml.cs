using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class MissionEditWindow : FluentWindow
{
    public MissionEditWindow(MissionEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser registered; nothing useful to do.
        }
        e.Handled = true;
    }
}
