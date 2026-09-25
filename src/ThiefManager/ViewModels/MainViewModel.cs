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
    private readonly ISettingsRepository _settingsRepository;
    private readonly IUpdateService _updateService;
    private List<FanMission> _allMissions = new();
    private List<Series> _allSeries = new();
    private List<SeriesPart> _allParts = new();
    private HashSet<GameTitle> _collapsedGames = new();

    public MainViewModel(
        IMissionRepository missionRepository,
        LaunchService launchService,
        IArchiveInstaller archiveInstaller,
        IFolderDeleter folderDeleter,
        ISeriesRepository seriesRepository,
        ISettingsRepository settingsRepository,
        IUpdateService updateService)
    {
        _missionRepository = missionRepository;
        _launchService = launchService;
        _archiveInstaller = archiveInstaller;
        _folderDeleter = folderDeleter;
        _seriesRepository = seriesRepository;
        _settingsRepository = settingsRepository;
        _updateService = updateService;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LaunchSelectedCommand = new RelayCommand(LaunchSelected, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.Installed);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedMission is not null);
        SetSelectedStatusCommand = new AsyncRelayCommand<MissionStatus>(SetSelectedStatusAsync, _ => SelectedMission is not null);
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedAsync, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.NotInstalled && SelectedMission.ArchivePath is not null);
        UninstallSelectedCommand = new AsyncRelayCommand(UninstallSelectedAsync, () => SelectedMission is not null && SelectedMission.InstallStatus == InstallStatus.Installed);
        ToggleSeriesExpandedCommand = new AsyncRelayCommand<SeriesHeaderRow?>(ToggleSeriesExpandedAsync);
        UngroupSelectedSeriesCommand = new AsyncRelayCommand(UngroupSelectedSeriesAsync, () => SelectedRow is SeriesHeaderRow);
        ToggleGameExpandedCommand = new AsyncRelayCommand<GameHeaderRow?>(ToggleGameExpandedAsync);
        SetSelectedRatingCommand = new AsyncRelayCommand<int?>(SetSelectedRatingAsync, _ => SelectedMission is not null);
        CollapseAllSeriesCommand = new AsyncRelayCommand(CollapseAllSeriesAsync);
        ExpandAllCommand = new AsyncRelayCommand(ExpandAllAsync);
        CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
    }

    public ObservableCollection<MissionListRow> VisibleRows { get; } = new();

    /// <summary>The missions currently shown (series members of collapsed series excluded), in row order.</summary>
    public IReadOnlyList<FanMission> VisibleMissions => VisibleRows.OfType<MissionRow>().Select(r => r.Mission).ToList();

    public bool IsSeriesHeaderSelected => SelectedRow is SeriesHeaderRow;

    public bool IsMissingPartSelected => SelectedRow is MissingPartRow;

    /// <summary>Mission-specific context menu items apply to mission rows (or no selection) only.</summary>
    public bool ShowMissionMenuItems => SelectedRow is not SeriesHeaderRow and not MissingPartRow and not GameHeaderRow;

    public bool IsGameHeaderSelected => SelectedRow is GameHeaderRow;

    public string? SelectedThiefGuildUrl => SelectedRow switch
    {
        MissingPartRow part => part.Part.ThiefGuildUrl,
        MissionRow mission => mission.Mission.ThiefGuildUrl,
        _ => null
    };

    public bool CanOpenSelectedOnThiefGuild => !string.IsNullOrWhiteSpace(SelectedThiefGuildUrl);

    public string? SelectedSeriesThiefGuildUrl => SelectedRow is SeriesHeaderRow { Series.ThiefGuildSeriesId: int seriesId }
        ? $"https://www.thiefguild.com/fanmissions?series={seriesId}"
        : null;

    public bool CanOpenSelectedSeriesOnThiefGuild => SelectedSeriesThiefGuildUrl is not null;

    /// <summary>True while the startup backfill or a manual refresh is fetching from Thief Guild.</summary>
    [ObservableProperty]
    private bool isThiefGuildRefreshRunning;

    public bool CanRefreshThiefGuild => !IsThiefGuildRefreshRunning;

    partial void OnIsThiefGuildRefreshRunningChanged(bool value) => OnPropertyChanged(nameof(CanRefreshThiefGuild));

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand LaunchSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand<MissionStatus> SetSelectedStatusCommand { get; }
    public IAsyncRelayCommand InstallSelectedCommand { get; }
    public IAsyncRelayCommand UninstallSelectedCommand { get; }
    public IAsyncRelayCommand<SeriesHeaderRow?> ToggleSeriesExpandedCommand { get; }
    public IAsyncRelayCommand UngroupSelectedSeriesCommand { get; }
    public IAsyncRelayCommand<GameHeaderRow?> ToggleGameExpandedCommand { get; }

    /// <summary>Sets the selected mission's 0–5 rating; a null parameter clears it.</summary>
    public IAsyncRelayCommand<int?> SetSelectedRatingCommand { get; }

    /// <summary>Collapses every series, leaving the game banners open.</summary>
    public IAsyncRelayCommand CollapseAllSeriesCommand { get; }

    /// <summary>Expands every series and every game.</summary>
    public IAsyncRelayCommand ExpandAllCommand { get; }

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    /// <summary>The version of a downloaded update waiting for "Restart to update", or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateReady))]
    private string? updateReadyVersion;

    [ObservableProperty]
    private string? updateReleaseNotes;

    /// <summary>The result of a manual "Check for Updates", shown in the About window.</summary>
    [ObservableProperty]
    private string? updateCheckMessage;

    public bool IsUpdateReady => UpdateReadyVersion is not null;

    /// <summary>
    /// The quiet check on launch: skipped for dev (non-installed) runs, and any failure (offline,
    /// GitHub unavailable) is swallowed because an automatic check should never nag.
    /// </summary>
    public async Task CheckForUpdatesOnStartupAsync()
    {
        if (!_updateService.IsInstalled)
            return;

        try
        {
            if (await _updateService.CheckAndDownloadAsync() is { } update)
                SetUpdateReady(update);
        }
        catch (Exception)
        {
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        if (!_updateService.IsInstalled)
        {
            UpdateCheckMessage = "Updates are only available in the installed version.";
            return;
        }

        UpdateCheckMessage = "Checking for updates…";
        try
        {
            var update = await _updateService.CheckAndDownloadAsync();
            if (update is null)
            {
                UpdateCheckMessage = $"You're up to date (version {AppVersion.Current}).";
            }
            else
            {
                SetUpdateReady(update);
                UpdateCheckMessage = $"Version {update.Version} is ready — restart to update.";
            }
        }
        catch (Exception ex)
        {
            UpdateCheckMessage = $"Couldn't check for updates: {ex.Message}";
        }
    }

    private void SetUpdateReady(AvailableUpdate update)
    {
        UpdateReleaseNotes = update.ReleaseNotesMarkdown;
        UpdateReadyVersion = update.Version;
    }

    /// <summary>
    /// Applies a downloaded update and restarts. Returns false, without doing anything, when a
    /// Thief Guild refresh is running and the caller hasn't confirmed — the window then asks the
    /// user. With no update ready there's nothing to do and it returns true.
    /// </summary>
    public bool TryRestartToUpdate(bool confirmedDespiteRefresh)
    {
        if (!IsUpdateReady)
            return true;
        if (IsThiefGuildRefreshRunning && !confirmedDespiteRefresh)
            return false;

        _updateService.ApplyAndRestart();
        return true;
    }

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
    private SortField sortField = SortField.Title;

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
        OnPropertyChanged(nameof(IsMissingPartSelected));
        OnPropertyChanged(nameof(IsGameHeaderSelected));
        OnPropertyChanged(nameof(ShowMissionMenuItems));
        OnPropertyChanged(nameof(SelectedThiefGuildUrl));
        OnPropertyChanged(nameof(CanOpenSelectedOnThiefGuild));
        OnPropertyChanged(nameof(SelectedSeriesThiefGuildUrl));
        OnPropertyChanged(nameof(CanOpenSelectedSeriesOnThiefGuild));
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
        SetSelectedRatingCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        _allMissions = await _missionRepository.GetAllAsync();
        _allSeries = await _seriesRepository.GetAllAsync();
        _allParts = await _seriesRepository.GetAllPartsAsync();
        var settings = await _settingsRepository.GetAsync();
        _collapsedGames = new HashSet<GameTitle>();
        if (settings.Thief1Collapsed)
            _collapsedGames.Add(GameTitle.Thief1);
        if (settings.Thief2Collapsed)
            _collapsedGames.Add(GameTitle.Thief2);
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        // Captured before Clear(): clearing makes the ListView push a null selection back.
        var selectedMissionId = SelectedMission?.Id;
        var selectedMissionSeriesId = SelectedMission?.SeriesId;
        var selectedMissionSeriesGame = SelectedMission?.Game;
        var selectedSeriesId = (SelectedRow as SeriesHeaderRow)?.Series.Id;
        var selectedSeriesGame = (SelectedRow as SeriesHeaderRow)?.CommonGame;
        var selectedGame = (SelectedRow as GameHeaderRow)?.Game;
        var selectedMissionGame = SelectedMission?.Game;

        var filtered = MissionQuery.Apply(_allMissions, GameFilter, StatusFilter, TagFilter, SortField, SortAscending, InstallStatusFilter, AuthorFilter).ToList();
        // Placeholders aren't missions, so only show them when no mission-level filter is active;
        // the Game filter is fine since it can't make a missing part less missing.
        var includeMissingParts = StatusFilter is null && InstallStatusFilter is null
            && string.IsNullOrWhiteSpace(TagFilter) && string.IsNullOrWhiteSpace(AuthorFilter);
        var rows = MissionListBuilder.Build(filtered, _allMissions, _allSeries, SortField, SortAscending, _allParts, includeMissingParts, collapsedGames: _collapsedGames);

        VisibleRows.Clear();
        foreach (var row in rows)
            VisibleRows.Add(row);

        SelectedRow = rows.FirstOrDefault(r => selectedMissionId is not null && r is MissionRow m && m.Mission.Id == selectedMissionId)
            ?? rows.FirstOrDefault(r => selectedSeriesId is not null && r is SeriesHeaderRow h && h.Series.Id == selectedSeriesId
                && (selectedSeriesGame is null || h.CommonGame == selectedSeriesGame))
            ?? rows.FirstOrDefault(r => selectedMissionSeriesId is not null && r is SeriesHeaderRow h2 && h2.Series.Id == selectedMissionSeriesId
                && (selectedMissionSeriesGame is null || h2.CommonGame == selectedMissionSeriesGame))
            ?? rows.FirstOrDefault(r => selectedGame is not null && r is GameHeaderRow g && g.Game == selectedGame)
            ?? rows.FirstOrDefault(r => selectedMissionGame is GameTitle g0 && _collapsedGames.Contains(g0) && r is GameHeaderRow g2 && g2.Game == g0);
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

        // An installed mission's folder goes too, so deleting it doesn't leave an orphaned FM on
        // disk. If the folder can't be removed, keep the library entry so nothing is lost silently.
        if (SelectedMission.InstallStatus == InstallStatus.Installed)
        {
            try
            {
                _folderDeleter.Delete(SelectedMission.FolderPath);
            }
            catch (Exception ex)
            {
                InstallError = $"Failed to delete: {ex.Message}";
                return;
            }
        }
        InstallError = null;

        var id = SelectedMission.Id;
        await _missionRepository.DeleteAsync(id);
        _allMissions.RemoveAll(m => m.Id == id);
        await _seriesRepository.DeleteOrphansAsync();
        _allSeries = await _seriesRepository.GetAllAsync();
        _allParts = await _seriesRepository.GetAllPartsAsync();
        SelectedMission = null;
        ApplyQuery();
    }

    private async Task SetSelectedRatingAsync(int? rating)
    {
        if (SelectedMission is null)
            return;

        SelectedMission.Rating = rating;
        await _missionRepository.UpdateAsync(SelectedMission);
        ApplyQuery();
    }

    private async Task CollapseAllSeriesAsync()
    {
        await _seriesRepository.SetAllExpandedAsync(false);
        foreach (var series in _allSeries)
            series.IsExpanded = false;
        ApplyQuery();
    }

    private async Task ExpandAllAsync()
    {
        await _seriesRepository.SetAllExpandedAsync(true);
        foreach (var series in _allSeries)
            series.IsExpanded = true;
        foreach (var game in _collapsedGames.ToList())
            await _settingsRepository.SetGameCollapsedAsync(game, false);
        _collapsedGames.Clear();
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
    /// Applies a successful Thief Guild lookup to a mission: see ThiefGuildMetadataApplier for which
    /// fields are overwritten versus only filled when blank, and SeriesAssigner for the series.
    /// </summary>
    public async Task ApplyThiefGuildMetadataAsync(FanMission mission, ThiefGuildLookupResult result)
    {
        ThiefGuildMetadataApplier.Apply(mission, result);
        await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
        await _missionRepository.UpdateAsync(mission);
        _allSeries = await _seriesRepository.GetAllAsync();
        _allParts = await _seriesRepository.GetAllPartsAsync();
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

    /// <summary>
    /// Copies a mission the Thief Guild backfill just fetched onto the loaded copy, using the same
    /// rules as IMissionRepository.ApplyThiefGuildMetadataAsync, so later whole-row saves from the
    /// list don't write stale Thief Guild data back. Returns true when the list needs rebuilding
    /// because the mission was newly placed into a series; rating, type and description changes
    /// wait for the reload at the end of the run rather than resetting the list's scroll mid-run.
    /// </summary>
    public bool ApplyFetchedThiefGuildMetadata(FanMission fetched)
    {
        var live = _allMissions.FirstOrDefault(m => m.Id == fetched.Id);
        if (live is null)
            return false;

        live.ThiefGuildRating = fetched.ThiefGuildRating;
        live.ThiefGuildRatingCount = fetched.ThiefGuildRatingCount;
        live.CampaignMissionCount = fetched.CampaignMissionCount;
        live.Description = fetched.Description;
        live.SequelOfTitle = fetched.SequelOfTitle;
        live.SequelOfUrl = fetched.SequelOfUrl;
        live.HasSequelTitle = fetched.HasSequelTitle;
        live.HasSequelUrl = fetched.HasSequelUrl;
        live.ThiefGuildMetadataVersion = fetched.ThiefGuildMetadataVersion;

        if (string.IsNullOrWhiteSpace(live.Author))
            live.Author = fetched.Author;
        if (live.ReleaseYear is null)
            live.ReleaseYear = fetched.ReleaseYear;
        if (string.IsNullOrWhiteSpace(live.Tags))
            live.Tags = fetched.Tags;

        var assignedSeries = false;
        if (live.SeriesId is null && !live.SeriesLookupChecked && fetched.SeriesId is not null)
        {
            live.SeriesId = fetched.SeriesId;
            live.SeriesPosition = fetched.SeriesPosition;
            assignedSeries = true;
        }
        live.SeriesLookupChecked |= fetched.SeriesLookupChecked;

        return assignedSeries;
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

    private async Task ToggleGameExpandedAsync(GameHeaderRow? header)
    {
        header ??= SelectedRow as GameHeaderRow;
        if (header is null)
            return;

        var collapse = !_collapsedGames.Contains(header.Game);
        if (collapse)
            _collapsedGames.Add(header.Game);
        else
            _collapsedGames.Remove(header.Game);
        await _settingsRepository.SetGameCollapsedAsync(header.Game, collapse);
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
