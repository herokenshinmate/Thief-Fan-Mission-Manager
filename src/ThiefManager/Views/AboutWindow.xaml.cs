using System.Windows;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class AboutWindow : FluentWindow
{
    private readonly Func<Task> _restartToUpdate;

    public AboutWindow(MainViewModel viewModel, Func<Task> restartToUpdate)
    {
        InitializeComponent();
        DataContext = viewModel;
        _restartToUpdate = restartToUpdate;
    }

    private async void RestartToUpdate_Click(object sender, RoutedEventArgs e) => await _restartToUpdate();
}
