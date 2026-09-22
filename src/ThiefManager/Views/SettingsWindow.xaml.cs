using System.Windows;
using Microsoft.Win32;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class SettingsWindow : FluentWindow
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void BrowseThief1FmFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(path => _viewModel.Thief1FmFolder = path);

    private void BrowseThief2FmFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(path => _viewModel.Thief2FmFolder = path);

    private void BrowseThief1ExePath_Click(object sender, RoutedEventArgs e) =>
        BrowseExecutable(path => _viewModel.Thief1ExePath = path);

    private void BrowseThief2ExePath_Click(object sender, RoutedEventArgs e) =>
        BrowseExecutable(path => _viewModel.Thief2ExePath = path);

    private void BrowseThief1DownloadsFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(path => _viewModel.Thief1DownloadsFolder = path);

    private void BrowseThief2DownloadsFolder_Click(object sender, RoutedEventArgs e) =>
        BrowseFolder(path => _viewModel.Thief2DownloadsFolder = path);

    private void BrowseFolder(Action<string> onSelected)
    {
        var dialog = new OpenFolderDialog { Title = "Select Folder" };
        if (dialog.ShowDialog(this) == true)
            onSelected(dialog.FolderName);
    }

    private void BrowseExecutable(Action<string> onSelected)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Executable",
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
            onSelected(dialog.FileName);
    }
}
