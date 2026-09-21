using System.Windows;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class MissionEditWindow : Window
{
    public MissionEditWindow(MissionEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
