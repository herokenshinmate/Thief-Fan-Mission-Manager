using System.Collections.ObjectModel;
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
    private List<FanMission> _allMissions = new();

    public MainViewModel(IMissionRepository missionRepository, LaunchService launchService)
    {
        _missionRepository = missionRepository;
        _launchService = launchService;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LaunchSelectedCommand = new RelayCommand(LaunchSelected, () => SelectedMission is not null);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, () => SelectedMission is not null);
        SetSelectedStatusCommand = new AsyncRelayCommand<MissionStatus>(SetSelectedStatusAsync, _ => SelectedMission is not null);
    }

    public ObservableCollection<FanMission> VisibleMissions { get; } = new();

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand LaunchSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand<MissionStatus> SetSelectedStatusCommand { get; }

    public string[] GameFilterOptions { get; } = { "(All)", "Thief1", "Thief2" };

    public string GameFilterDisplay
    {
        get => GameFilter?.ToString() ?? "(All)";
        set => GameFilter = value == "(All)" ? null : Enum.Parse<GameTitle>(value);
    }

    [ObservableProperty]
    private GameTitle? gameFilter;

    public string[] StatusFilterOptions { get; } = { "(All)", "NotPlayed", "InProgress", "Completed", "Abandoned" };

    public string StatusFilterDisplay
    {
        get => StatusFilter?.ToString() ?? "(All)";
        set => StatusFilter = value == "(All)" ? null : Enum.Parse<MissionStatus>(value);
    }

    [ObservableProperty]
    private MissionStatus? statusFilter;

    [ObservableProperty]
    private string? tagFilter;

    public string[] SortFieldOptions { get; } = { "Title", "Game", "Status", "Rating" };

    public string SortFieldDisplay
    {
        get => SortField.ToString();
        set => SortField = Enum.Parse<SortField>(value);
    }

    [ObservableProperty]
    private SortField sortField = SortField.Title;

    [ObservableProperty]
    private bool sortAscending = true;

    [ObservableProperty]
    private FanMission? selectedMission;

    [ObservableProperty]
    private string? launchError;

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
    partial void OnTagFilterChanged(string? value) => ApplyQuery();
    partial void OnSortFieldChanged(SortField value)
    {
        OnPropertyChanged(nameof(SortFieldDisplay));
        ApplyQuery();
    }
    partial void OnSortAscendingChanged(bool value) => ApplyQuery();

    partial void OnSelectedMissionChanged(FanMission? value)
    {
        LaunchSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        SetSelectedStatusCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        _allMissions = await _missionRepository.GetAllAsync();
        ApplyQuery();
    }

    private void ApplyQuery()
    {
        var filtered = MissionQuery.Apply(_allMissions, GameFilter, StatusFilter, TagFilter, SortField, SortAscending);
        VisibleMissions.Clear();
        foreach (var mission in filtered)
            VisibleMissions.Add(mission);
    }

    private void LaunchSelected()
    {
        if (SelectedMission is null)
            return;

        var exePath = SelectedMission.Game == GameTitle.Thief1 ? _thief1ExePath : _thief2ExePath;
        var result = _launchService.Launch(exePath);
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

        await _missionRepository.DeleteAsync(SelectedMission.Id);
        _allMissions.RemoveAll(m => m.Id == SelectedMission.Id);
        SelectedMission = null;
        ApplyQuery();
    }

    private async Task SetSelectedStatusAsync(MissionStatus status)
    {
        if (SelectedMission is null)
            return;

        SelectedMission.Status = status;
        await _missionRepository.UpdateAsync(SelectedMission);
        ApplyQuery();
    }
}
