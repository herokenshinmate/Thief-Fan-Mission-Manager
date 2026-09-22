using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThiefManager.Data;
using ThiefManager.ViewModels;
using ThiefManager.Views;
using Wpf.Ui.Controls;

namespace ThiefManager;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IMissionRepository _missionRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly Services.LaunchService _launchService;
    private readonly Services.IDirectoryReader _directoryReader;

    public MainWindow(
        MainViewModel viewModel,
        IMissionRepository missionRepository,
        ISettingsRepository settingsRepository,
        Services.LaunchService launchService,
        Services.IDirectoryReader directoryReader)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _missionRepository = missionRepository;
        _settingsRepository = settingsRepository;
        _launchService = launchService;
        _directoryReader = directoryReader;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void AddMission_Click(object sender, RoutedEventArgs e)
    {
        var editViewModel = new MissionEditViewModel(_missionRepository);
        var editWindow = new MissionEditWindow(editViewModel) { Owner = this };
        editViewModel.Saved += async (_, _) =>
        {
            editWindow.Close();
            await _viewModel.LoadCommand.ExecuteAsync(null);
        };
        editWindow.ShowDialog();
    }

    private void MissionList_DoubleClick(object sender, MouseButtonEventArgs e) =>
        OpenPropertiesForSelectedMission();

    private void MissionProperties_Click(object sender, RoutedEventArgs e) =>
        OpenPropertiesForSelectedMission();

    private void OpenPropertiesForSelectedMission()
    {
        if (_viewModel.SelectedMission is null)
            return;

        var editViewModel = new MissionEditViewModel(_missionRepository);
        editViewModel.LoadFrom(_viewModel.SelectedMission);
        var editWindow = new MissionEditWindow(editViewModel) { Owner = this };
        editViewModel.Saved += async (_, _) =>
        {
            editWindow.Close();
            await _viewModel.LoadCommand.ExecuteAsync(null);
        };
        editWindow.ShowDialog();
    }

    private void MissionList_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.ListViewItem>(source) is { } item)
            item.IsSelected = true;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsViewModel = new SettingsViewModel(_settingsRepository);
        await settingsViewModel.LoadCommand.ExecuteAsync(null);
        var settingsWindow = new SettingsWindow(settingsViewModel) { Owner = this };
        settingsViewModel.Saved += async (_, _) =>
        {
            var settings = await _settingsRepository.GetAsync();
            _viewModel.ConfigureExePaths(settings.Thief1ExePath, settings.Thief2ExePath);
            Services.GameIconStore.UpdatePaths(settings.Thief1ExePath, settings.Thief2ExePath);
            _viewModel.RefreshGameIcons();
            await _viewModel.LoadCommand.ExecuteAsync(null);
            settingsWindow.Close();
        };
        settingsWindow.ShowDialog();
    }

    private async void OpenScan_Click(object sender, RoutedEventArgs e)
    {
        var settings = await _settingsRepository.GetAsync();
        var scanViewModel = new ScanViewModel(_directoryReader, _missionRepository);
        var scanWindow = new ScanWindow(scanViewModel, settings) { Owner = this };
        scanWindow.Closed += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
        scanWindow.ShowDialog();
    }
}
