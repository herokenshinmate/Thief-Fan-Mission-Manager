using System.Windows;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
