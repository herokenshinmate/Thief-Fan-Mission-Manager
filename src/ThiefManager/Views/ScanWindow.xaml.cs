using System.Windows;
using ThiefManager.Models;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class ScanWindow : FluentWindow
{
    private readonly ScanViewModel _viewModel;
    private readonly AppSettings _settings;

    public ScanWindow(ScanViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        ScanThief1Button.IsEnabled = !string.IsNullOrWhiteSpace(settings.Thief1FmFolder);
        ScanThief2Button.IsEnabled = !string.IsNullOrWhiteSpace(settings.Thief2FmFolder);
        InstallThief1DownloadsButton.IsEnabled = !string.IsNullOrWhiteSpace(settings.Thief1DownloadsFolder) && !string.IsNullOrWhiteSpace(settings.Thief1FmFolder);
        InstallThief2DownloadsButton.IsEnabled = !string.IsNullOrWhiteSpace(settings.Thief2DownloadsFolder) && !string.IsNullOrWhiteSpace(settings.Thief2FmFolder);
    }

    private async void ScanThief1_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Thief1FmFolder))
            await _viewModel.Scan(GameTitle.Thief1, _settings.Thief1FmFolder);
    }

    private async void ScanThief2_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Thief2FmFolder))
            await _viewModel.Scan(GameTitle.Thief2, _settings.Thief2FmFolder);
    }

    private async void InstallThief1Downloads_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Thief1DownloadsFolder) && !string.IsNullOrWhiteSpace(_settings.Thief1FmFolder))
            await _viewModel.ScanDownloads(GameTitle.Thief1, _settings.Thief1DownloadsFolder, _settings.Thief1FmFolder);
    }

    private async void InstallThief2Downloads_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Thief2DownloadsFolder) && !string.IsNullOrWhiteSpace(_settings.Thief2FmFolder))
            await _viewModel.ScanDownloads(GameTitle.Thief2, _settings.Thief2DownloadsFolder, _settings.Thief2FmFolder);
    }
}
