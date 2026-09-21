using System.Windows;
using ThiefManager.Models;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class ScanWindow : Window
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
}
