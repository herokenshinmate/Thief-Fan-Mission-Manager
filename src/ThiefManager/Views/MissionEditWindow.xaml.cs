using System.Windows;
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
}
