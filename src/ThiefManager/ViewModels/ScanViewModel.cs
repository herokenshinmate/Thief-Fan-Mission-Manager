using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IMissionRepository _missionRepository;
    private GameTitle _lastScannedGame;

    public ScanViewModel(IDirectoryReader directoryReader, IMissionRepository missionRepository)
    {
        _directoryReader = directoryReader;
        _missionRepository = missionRepository;
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync);
    }

    public ObservableCollection<ScanCandidateViewModel> Candidates { get; } = new();
    public IAsyncRelayCommand ImportSelectedCommand { get; }

    public async Task Scan(GameTitle game, string fmFolder)
    {
        _lastScannedGame = game;
        var existing = await _missionRepository.GetAllAsync();
        var subfolders = _directoryReader.GetSubdirectories(fmFolder);
        var newCandidates = ScanService.FindNewCandidates(subfolders, existing.Select(m => m.FolderPath));

        Candidates.Clear();
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(candidate.SuggestedTitle, candidate.FolderPath));
    }

    private async Task ImportSelectedAsync()
    {
        var selected = Candidates.Where(c => c.IsSelected).ToList();
        foreach (var candidate in selected)
        {
            await _missionRepository.AddAsync(new FanMission
            {
                Title = candidate.SuggestedTitle,
                Game = _lastScannedGame,
                FolderPath = candidate.FolderPath
            });
            Candidates.Remove(candidate);
        }
    }
}
