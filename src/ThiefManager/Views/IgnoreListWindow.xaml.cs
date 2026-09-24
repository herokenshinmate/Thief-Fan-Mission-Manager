using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class IgnoreListWindow : FluentWindow
{
    public IgnoreListWindow(IgnoreListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
