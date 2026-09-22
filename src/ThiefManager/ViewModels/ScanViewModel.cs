using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IArchiveFileReader _archiveFileReader;
    private readonly IMissionRepository _missionRepository;
    private GameTitle _lastScannedGame;

    public ScanViewModel(IDirectoryReader directoryReader, IArchiveFileReader archiveFileReader, IMissionRepository missionRepository)
    {
        _directoryReader = directoryReader;
        _archiveFileReader = archiveFileReader;
        _missionRepository = missionRepository;
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync);
    }

    public ObservableCollection<ScanCandidateViewModel> Candidates { get; } = new();
    public IAsyncRelayCommand ImportSelectedCommand { get; }

    [ObservableProperty] private string? scanError;

    public async Task Scan(GameTitle game, string fmFolder)
    {
        ScanError = null;
        _lastScannedGame = game;
        var existing = await _missionRepository.GetAllAsync();

        IReadOnlyList<string> subfolders;
        try
        {
            subfolders = _directoryReader.GetSubdirectories(fmFolder);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ScanError = $"Could not read folder: {fmFolder}";
            Candidates.Clear();
            return;
        }

        var newCandidates = ScanService.FindNewCandidates(subfolders, existing.Select(m => m.FolderPath));

        Candidates.Clear();
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(candidate.SuggestedTitle, candidate.FolderPath));
    }

    public async Task ScanDownloads(GameTitle game, string downloadsFolder, string fmFolder)
    {
        ScanError = null;
        _lastScannedGame = game;
        var existing = await _missionRepository.GetAllAsync();

        IReadOnlyList<string> archiveFiles;
        try
        {
            archiveFiles = _archiveFileReader.GetArchiveFiles(downloadsFolder);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ScanError = $"Could not read folder: {downloadsFolder}";
            Candidates.Clear();
            return;
        }

        var existingArchivePaths = existing.Where(m => m.ArchivePath is not null).Select(m => m.ArchivePath!);
        var newCandidates = DownloadedArchiveScanner.FindNewArchives(archiveFiles, fmFolder, existingArchivePaths);

        Candidates.Clear();
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(candidate.SuggestedTitle, candidate.TargetFolderPath, candidate.ArchivePath));
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
                FolderPath = candidate.FolderPath,
                ArchivePath = candidate.ArchivePath,
                InstallStatus = candidate.ArchivePath is null ? InstallStatus.Installed : InstallStatus.NotInstalled
            });
            Candidates.Remove(candidate);
        }
    }
}
