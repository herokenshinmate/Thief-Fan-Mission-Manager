using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThiefManager.Data;
using ThiefManager.Models;
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
    private readonly Services.IArchiveFileReader _archiveFileReader;
    private readonly Services.IThiefGuildLookupService _thiefGuildLookupService;

    public MainWindow(
        MainViewModel viewModel,
        IMissionRepository missionRepository,
        ISettingsRepository settingsRepository,
        Services.LaunchService launchService,
        Services.IDirectoryReader directoryReader,
        Services.IArchiveFileReader archiveFileReader,
        Services.IThiefGuildLookupService thiefGuildLookupService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _missionRepository = missionRepository;
        _settingsRepository = settingsRepository;
        _launchService = launchService;
        _directoryReader = directoryReader;
        _archiveFileReader = archiveFileReader;
        _thiefGuildLookupService = thiefGuildLookupService;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void AddMission_Click(object sender, RoutedEventArgs e)
    {
        var editViewModel = new MissionEditViewModel(_missionRepository, _thiefGuildLookupService);
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
        if (_viewModel.SelectedMission is not null)
            OpenPropertiesFor(_viewModel.SelectedMission);
    }

    private void OpenPropertiesFor(FanMission mission)
    {
        var editViewModel = new MissionEditViewModel(_missionRepository, _thiefGuildLookupService);
        editViewModel.LoadFrom(mission);
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
        var scanViewModel = new ScanViewModel(_directoryReader, _archiveFileReader, _missionRepository);
        var scanWindow = new ScanWindow(scanViewModel, settings) { Owner = this };
        scanWindow.Closed += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
        scanWindow.ShowDialog();
    }

    private async void QuickScanDownloads_Click(object sender, RoutedEventArgs e)
    {
        var settings = await _settingsRepository.GetAsync();
        var scanViewModel = new ScanViewModel(_directoryReader, _archiveFileReader, _missionRepository);
        var scanWindow = new ScanWindow(scanViewModel, settings) { Owner = this };
        await scanViewModel.ScanAllDownloads(settings);
        scanWindow.Closed += async (_, _) => await _viewModel.LoadCommand.ExecuteAsync(null);
        scanWindow.ShowDialog();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var mission = _viewModel.SelectedMission;
        if (mission is null)
            return;

        await _viewModel.InstallSelectedCommand.ExecuteAsync(null);

        if (mission.InstallStatus != InstallStatus.Installed)
            return; // extraction failed; InstallError is already shown

        if (!string.IsNullOrWhiteSpace(mission.ThiefGuildUrl) || mission.ThiefGuildLookupDismissed)
            return; // already linked, or the user asked not to be asked again

        var result = await _thiefGuildLookupService.SearchByTitleAsync(mission.Title);
        if (result is not null)
        {
            await _viewModel.ApplyThiefGuildMetadataAsync(mission, result);
            return;
        }

        var notFoundDialog = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = "Thief Guild Lookup",
            Content = $"Couldn't find \"{mission.Title}\" on Thief Guild.\n\nYou can paste its Thief Guild page URL in Properties later, or dismiss this so it's never checked again for this mission.",
            PrimaryButtonText = "Enter URL...",
            CloseButtonText = "Dismiss"
        };

        var notFoundResult = await notFoundDialog.ShowDialogAsync();

        if (notFoundResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
            OpenPropertiesFor(mission);
        else
            await _viewModel.DismissThiefGuildLookupAsync(mission);
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var mission = _viewModel.SelectedMission;
        if (mission is null)
            return;

        var reinstallNote = mission.ArchivePath is null
            ? "This mission has no known source archive on record, so it can't be reinstalled automatically afterward."
            : "You can reinstall it later from the same downloaded archive.";

        var confirmDialog = new Wpf.Ui.Controls.MessageBox
        {
            Owner = this,
            Title = "Uninstall Mission",
            Content = $"Uninstall \"{mission.Title}\"?\n\nThis permanently deletes its folder from disk:\n{mission.FolderPath}\n\n{reinstallNote}",
            PrimaryButtonText = "Uninstall",
            PrimaryButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger,
            CloseButtonText = "Cancel"
        };

        var result = await confirmDialog.ShowDialogAsync();

        if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
            await _viewModel.UninstallSelectedCommand.ExecuteAsync(null);
    }
}
