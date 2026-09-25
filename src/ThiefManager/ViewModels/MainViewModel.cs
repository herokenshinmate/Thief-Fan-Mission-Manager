using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IMissionRepository _missionRepository;
    private readonly LaunchService _launchService;
    private readonly IArchiveInstaller _archiveInstaller;
    private readonly IFolderDeleter _folderDeleter;
    private readonly ISeriesRepository _seriesRepository;
    private List<FanMission> _allMissions = new();
    private List<Series> _allSeries = new();

    public MainViewModel(
        IMissionRepository missionRepository,
        LaunchService launchService,
        IArchiveInstaller archiveInstaller,
        IFolderDeleter folderDeleter,
        ISeriesRepository seriesRepository)
    {
        _missionRepository = missionRepository;
        _launchService = launchService;
        _archiveInstaller = archiveInstaller;
        _folderDeleter = folderDeleter;
        _seriesRepository = seriesRepository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LaunchSelectedCommand = new RelayCommand(LaunchSelected, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.Installed);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedMission is not null);
        SetSelectedStatusCommand = new AsyncRelayCommand<MissionStatus>(SetSelectedStatusAsync, _ => SelectedMission is not null);
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.NotInstalled && SelectedMission.ArchivePath is not null);
        UninstallSelectedCommand = new AsyncRelayCommand(UninstallSelectedAsync, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.Installed);
        ToggleSeriesExpandedCommand = new AsyncRelayCommand<SeriesHeaderRow?>(ToggleSeriesExpandedAsync);
        UngroupSelectedSeriesCommand = new AsyncRelayCommand(UngroupSelectedSeriesAsync, () => SelectedRow is SeriesHeaderRow);
    }

    public ObservableCollection<MissionListRow> VisibleRows { get; } = new();

    /// <summary>The missions currently shown (series members of collapsed series excluded), in row order.</summary>
    public IReadOnlyList<FanMission> VisibleMissions => VisibleRows.OfType<MissionRow>().Select(r => r.Mission).ToList();

    public bool IsSeriesHeaderSelected => SelectedRow is SeriesHeaderRow;

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand LaunchSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand<MissionStatus> SetSelectedStatusCommand { get; }
    public IAsyncRelayCommand InstallSelectedCommand { get; }
    public IAsyncRelayCommand UninstallSelectedCommand { get; }
    public IAsyncRelayCommand<SeriesHeaderRow?> ToggleSeriesExpandedCommand { get; }
    public IAsyncRelayCommand UngroupSelectedSeriesCommand { get; }

    public string[] GameFilterOptions { get; private set; } = { "(All)", GameTitleNames.Thief1DisplayName, GameTitleNames.Thief2DisplayName };

    public void RefreshGameIcons()
    {
        GameFilterOptions = GameFilterOptions.ToArray();
        OnPropertyChanged(nameof(GameFilterOptions));
    }

    public string GameFilterDisplay
    {
        get => GameFilter?.ToDisplayName() ?? "(All)";
        set => GameFilter = value == "(All)" ? null : GameTitleNames.Parse(value);
    }

    [ObservableProperty]
    private GameTitle? gameFilter;

    public string[] StatusFilterOptions { get; } =
    {
        "(All)",
        MissionStatusNames.NotPlayedDisplayName,
        MissionStatusNames.InProgressDisplayName,
        MissionStatusNames.CompletedDisplayName,
        MissionStatusNames.AbandonedDisplayName
    };

    public string StatusFilterDisplay
    {
        get => StatusFilter?.ToDisplayName() ?? "(All)";
        set => StatusFilter = value == "(All)" ? null : MissionStatusNames.Parse(value);
    }

    [ObservableProperty]
    private MissionStatus? statusFilter;

    public string[] InstallStatusFilterOptions { get; } =
    {
        "(All)",
        InstallStatusNames.InstalledDisplayName,
        InstallStatusNames.NotInstalledDisplayName
    };

    public string InstallStatusFilterDisplay
    {
        get => InstallStatusFilter?.ToDisplayName() ?? "(All)";
        set => InstallStatusFilter = value == "(All)" ? null : InstallStatusNames.Parse(value);
    }

    [ObservableProperty]
    private InstallStatus? installStatusFilter;

    [ObservableProperty]
    private string? tagFilter;

    [ObservableProperty]
    private string? authorFilter;

    private static readonly (SortField Field, string Name)[] SortFieldNames =
    {
        (SortField.Title, "Title"),
        (SortField.Game, "Game"),
        (SortField.Status, "Status"),
        (SortField.InstallStatus, "Install Status"),
        (SortField.Rating, "Rating"),
        (SortField.Author, "Author"),
        (SortField.Tags, "Tags"),
        (SortField.ThiefGuildRating, "TG Rating"),
        (SortField.MissionType, "Type")
    };

    public string[] SortFieldOptions { get; } = SortFieldNames.Select(n => n.Name).ToArray();

    public string SortFieldDisplay
    {
        get => SortFieldNames.First(n => n.Field == SortField).Name;
        set => SortField = SortFieldNames.First(n => n.Name == value).Field;
    }

    [ObservableProperty]
    private SortField sortField = SortField.Game;

    [ObservableProperty]
    private bool sortAscending = true;

    [ObservableProperty]
    private FanMission? selectedMission;

    [ObservableProperty]
    private MissionListRow? selectedRow;

    /// <summary>Progress of background work (the Thief Guild series backfill), shown in the status bar.</summary>
    [ObservableProperty]
    private string? backgroundStatus;

    [ObservableProperty]
    private string? launchError;

    [ObservableProperty]
    private string? installError;

    partial void OnGameFilterChanged(GameTitle? value)
    {
        OnPropertyChanged(nameof(GameFilterDisplay));
        ApplyQuery();
    }
    partial void OnStatusFilterChanged(MissionStatus? value)
    {
        OnPropertyChanged(nameof(StatusFilterDisplay));
        ApplyQuery();
    }
    partial void OnInstallStatusFilterChanged(InstallStatus? value)
    {
        OnPropertyChanged(nameof(InstallStatusFilterDisplay));
        ApplyQuery();
    }
    partial void OnTagFilterChanged(string? value) => ApplyQuery();
    partial void OnAuthorFilterChanged(string? value) => ApplyQuery();
    partial void OnSortFieldChanged(SortField value)
    {
        OnPropertyChanged(nameof(SortFieldDisplay));
        ApplyQuery();
    }
    partial void OnSortAscendingChanged(bool value) => ApplyQuery();

    partial void OnSelectedRowChanged(MissionListRow? value)
    {
        SelectedMission = (value as MissionRow)?.Mission;
        OnPropertyChanged(nameof(IsSeriesHeaderSelected));
        UngroupSelectedSeriesCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMissionChanged(FanMission? value)
    {
        if (value is null)
        {
            if (SelectedRow is MissionRow)
                SelectedRow = null;
        }
        else if (SelectedRow is not MissionRow row || !ReferenceEquals(row.Mission, value))
        {
            var match = VisibleRows.OfType<MissionRow>().FirstOrDefault(r => ReferenceEquals(r.Mission, value));
            if (match is not null)
                SelectedRow = match;
        }

        NotifyMissionCommandsCanExecuteChanged();
    }

    private void NotifyMissionCommandsCanExecuteChanged()
    {
        LaunchSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        SetSelectedStatusCommand.NotifyCanExecuteChanged();
        InstallSelectedCommand.NotifyCanExecuteChanged();
        UninstallSelectedCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        _allMissions = await _missionRepository.GetAllAsync();
        _allSeries = await _seriesRepository.GetAllAsync();
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        // Captured before Clear(): clearing makes the ListView push a null selection back.
        var selectedMissionId = SelectedMission?.Id;
        var selectedMissionSeriesId = SelectedMission?.SeriesId;
        var selectedSeriesId = (SelectedRow as SeriesHeaderRow)?.Series.Id;

        var filtered = MissionQuery.Apply(_allMissions, GameFilter, StatusFilter, TagFilter, SortField, SortAscending, InstallStatusFilter, AuthorFilter).ToList();
        var rows = MissionListBuilder.Build(filtered, _allMissions, _allSeries, SortField, SortAscending);

        VisibleRows.Clear();
        foreach (var row in rows)
            VisibleRows.Add(row);

        SelectedRow = rows.FirstOrDefault(r => selectedMissionId is not null && r is MissionRow m && m.Mission.Id == selectedMissionId)
            ?? rows.FirstOrDefault(r => selectedSeriesId is not null && r is SeriesHeaderRow h && h.Series.Id == selectedSeriesId)
            ?? rows.FirstOrDefault(r => selectedMissionSeriesId is not null && r is SeriesHeaderRow h2 && h2.Series.Id == selectedMissionSeriesId);
    }

    private void LaunchSelected()
    {
        if (SelectedMission is null)
            return;

        var exePath = SelectedMission.Game == GameTitle.Thief1 ? _thief1ExePath : _thief2ExePath;
        var fmFolderName = Path.GetFileName(SelectedMission.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var result = _launchService.Launch(exePath, fmFolderName);
        LaunchError = result.Ok ? null : result.Error;
    }

    private string? _thief1ExePath;
    private string? _thief2ExePath;

    public void ConfigureExePaths(string? thief1ExePath, string? thief2ExePath)
    {
        _thief1ExePath = thief1ExePath;
        _thief2ExePath = thief2ExePath;
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedMission is null)
            return;

        var id = SelectedMission.Id;
        await _missionRepository.DeleteAsync(id);
        _allMissions.RemoveAll(m => m.Id == id);
        await _seriesRepository.DeleteOrphansAsync();
        _allSeries = await _seriesRepository.GetAllAsync();
        SelectedMission = null;
        ApplyQuery();
    }

    private async Task SetSelectedStatusAsync(MissionStatus status)
    {
        if (SelectedMission is null)
            return;

        MissionStatusDates.Apply(SelectedMission, status, DateTime.Now);
        await _missionRepository.UpdateAsync(SelectedMission);
        ApplyQuery();
    }

    private async Task InstallSelectedAsync()
    {
        if (SelectedMission is null || SelectedMission.InstallStatus != InstallStatus.NotInstalled || SelectedMission.ArchivePath is null)
            return;

        try
        {
            _archiveInstaller.Install(SelectedMission.ArchivePath, SelectedMission.FolderPath);
            SelectedMission.InstallStatus = InstallStatus.Installed;
            await _missionRepository.UpdateAsync(SelectedMission);
            InstallError = null;
        }
        catch (Exception ex)
        {
            InstallError = $"Failed to install: {ex.Message}";
        }

        NotifyMissionCommandsCanExecuteChanged();
        ApplyQuery();
    }

    private async Task UninstallSelectedAsync()
    {
        if (SelectedMission is null || SelectedMission.InstallStatus != InstallStatus.Installed)
            return;

        try
        {
            _folderDeleter.Delete(SelectedMission.FolderPath);
            SelectedMission.InstallStatus = InstallStatus.NotInstalled;
            await _missionRepository.UpdateAsync(SelectedMission);
            InstallError = null;
        }
        catch (Exception ex)
        {
            InstallError = $"Failed to uninstall: {ex.Message}";
        }

        NotifyMissionCommandsCanExecuteChanged();
        ApplyQuery();
    }

    /// <summary>
    /// Applies a successful Thief Guild lookup to a mission: fills in only the fields that
    /// are currently blank (never overwriting anything already entered), assigns it to a series
    /// when Thief Guild found one, and records the matched URL so it isn't looked up again.
    /// </summary>
    public async Task ApplyThiefGuildMetadataAsync(FanMission mission, ThiefGuildLookupResult result)
    {
        if (string.IsNullOrWhiteSpace(mission.Author))
            mission.Author = result.Author;
        if (mission.ReleaseYear is null)
            mission.ReleaseYear = result.ReleaseYear;
        if (string.IsNullOrWhiteSpace(mission.Tags))
            mission.Tags = result.Tags;
        mission.ThiefGuildUrl = result.Url;

        await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
        await _missionRepository.UpdateAsync(mission);
        _allSeries = await _seriesRepository.GetAllAsync();
        ApplyQuery();
    }

    /// <summary>
    /// Records that a failed Thief Guild lookup was dismissed, so it is never auto-retried.
    /// </summary>
    public async Task DismissThiefGuildLookupAsync(FanMission mission)
    {
        mission.ThiefGuildLookupDismissed = true;
        await _missionRepository.UpdateAsync(mission);
    }

    private async Task ToggleSeriesExpandedAsync(SeriesHeaderRow? header)
    {
        header ??= SelectedRow as SeriesHeaderRow;
        if (header is null)
            return;

        var isExpanded = !header.Series.IsExpanded;
        await _seriesRepository.SetExpandedAsync(header.Series.Id, isExpanded);
        header.Series.IsExpanded = isExpanded;
        ApplyQuery();
    }

    public async Task RenameSeriesAsync(Series series, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return;

        await _seriesRepository.RenameAsync(series.Id, newName);
        await LoadAsync();
    }

    private async Task UngroupSelectedSeriesAsync()
    {
        if (SelectedRow is not SeriesHeaderRow header)
            return;

        await _seriesRepository.DeleteAsync(header.Series.Id);
        SelectedRow = null;
        await LoadAsync();
    }
}
