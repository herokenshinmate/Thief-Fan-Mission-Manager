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
    }

    public ObservableCollection<FanMission> VisibleMissions { get; } = new();

    public IAsyncRelayCommand LoadCommand { get; }
    public IRelayCommand LaunchSelectedCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }

    [ObservableProperty]
    private GameTitle? gameFilter;

    [ObservableProperty]
    private MissionStatus? statusFilter;

    [ObservableProperty]
    private string? tagFilter;

    [ObservableProperty]
    private SortField sortField = SortField.Title;

    [ObservableProperty]
    private bool sortAscending = true;

    [ObservableProperty]
    private FanMission? selectedMission;

    [ObservableProperty]
    private string? launchError;

    partial void OnGameFilterChanged(GameTitle? value) => ApplyQuery();
    partial void OnStatusFilterChanged(MissionStatus? value) => ApplyQuery();
    partial void OnTagFilterChanged(string? value) => ApplyQuery();
    partial void OnSortFieldChanged(SortField value) => ApplyQuery();
    partial void OnSortAscendingChanged(bool value) => ApplyQuery();

    partial void OnSelectedMissionChanged(FanMission? value)
    {
        LaunchSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
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
}
