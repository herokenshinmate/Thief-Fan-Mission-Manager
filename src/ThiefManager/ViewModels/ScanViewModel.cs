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
    private readonly IIgnoredFmRepository _ignoredFmRepository;

    public ScanViewModel(
        IDirectoryReader directoryReader,
        IArchiveFileReader archiveFileReader,
        IMissionRepository missionRepository,
        IIgnoredFmRepository ignoredFmRepository)
    {
        _directoryReader = directoryReader;
        _archiveFileReader = archiveFileReader;
        _missionRepository = missionRepository;
        _ignoredFmRepository = ignoredFmRepository;
        ImportSelectedCommand = new AsyncRelayCommand(ImportSelectedAsync);
        IgnoreCandidateCommand = new AsyncRelayCommand<ScanCandidateViewModel>(IgnoreCandidateAsync);
    }

    public ObservableCollection<ScanCandidateViewModel> Candidates { get; } = new();
    public IAsyncRelayCommand ImportSelectedCommand { get; }
    public IAsyncRelayCommand<ScanCandidateViewModel> IgnoreCandidateCommand { get; }

    [ObservableProperty] private string? scanError;

    public async Task Scan(GameTitle game, string fmFolder)
    {
        ScanError = null;
        var existing = await _missionRepository.GetAllAsync();
        var ignoredNames = await GetIgnoredNamesAsync(game);

        IReadOnlyList<string> subfolders;
        try
        {
            subfolders = _directoryReader.GetSubdirectories(fmFolder);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ScanError = $"Could not read folder: {fmFolder}";
            return;
        }

        var newCandidates = ScanService.FindNewCandidates(subfolders, existing.Select(m => m.FolderPath), ignoredNames);

        RemoveCandidates(c => c.Game == game && c.ArchivePath is null);
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(game, candidate.SuggestedTitle, candidate.FolderPath));
    }

    public async Task ScanDownloads(GameTitle game, string downloadsFolder, string fmFolder)
    {
        ScanError = null;
        var existing = await _missionRepository.GetAllAsync();
        var ignoredNames = await GetIgnoredNamesAsync(game);

        IReadOnlyList<string> archiveFiles;
        try
        {
            archiveFiles = _archiveFileReader.GetArchiveFiles(downloadsFolder);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ScanError = $"Could not read folder: {downloadsFolder}";
            return;
        }

        var existingArchivePaths = existing.Where(m => m.ArchivePath is not null).Select(m => m.ArchivePath!);
        var existingInstalledNames = existing
            .Where(m => m.Game == game)
            .Select(m => Path.GetFileName(m.FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Concat(existing.Where(m => m.Game == game).Select(m => m.Title));
        var newCandidates = DownloadedArchiveScanner.FindNewArchives(archiveFiles, fmFolder, existingArchivePaths, existingInstalledNames, ignoredNames);

        RemoveCandidates(c => c.Game == game && c.ArchivePath is not null);
        foreach (var candidate in newCandidates)
            Candidates.Add(new ScanCandidateViewModel(game, candidate.SuggestedTitle, candidate.TargetFolderPath, candidate.ArchivePath));
    }

    private async Task<IReadOnlyList<string>> GetIgnoredNamesAsync(GameTitle game)
    {
        var ignored = await _ignoredFmRepository.GetAllAsync();
        return ignored.Where(i => i.Game == game).Select(i => i.Name).ToList();
    }

    /// <summary>
    /// Scans both games' configured Downloads folders in one pass (whichever are configured),
    /// combining results into Candidates for a single review-and-import pass.
    /// </summary>
    public async Task ScanAllDownloads(AppSettings settings)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Thief1DownloadsFolder) && !string.IsNullOrWhiteSpace(settings.Thief1FmFolder))
        {
            await ScanDownloads(GameTitle.Thief1, settings.Thief1DownloadsFolder, settings.Thief1FmFolder);
            if (ScanError is not null)
                errors.Add(ScanError);
        }

        if (!string.IsNullOrWhiteSpace(settings.Thief2DownloadsFolder) && !string.IsNullOrWhiteSpace(settings.Thief2FmFolder))
        {
            await ScanDownloads(GameTitle.Thief2, settings.Thief2DownloadsFolder, settings.Thief2FmFolder);
            if (ScanError is not null)
                errors.Add(ScanError);
        }

        ScanError = errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    /// <summary>
    /// Scans both games' configured FM folders in one pass (whichever are configured),
    /// combining results into Candidates for a single review-and-import pass.
    /// </summary>
    public async Task ScanAllInstalled(AppSettings settings)
    {
        var errors = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.Thief1FmFolder))
        {
            await Scan(GameTitle.Thief1, settings.Thief1FmFolder);
            if (ScanError is not null)
                errors.Add(ScanError);
        }

        if (!string.IsNullOrWhiteSpace(settings.Thief2FmFolder))
        {
            await Scan(GameTitle.Thief2, settings.Thief2FmFolder);
            if (ScanError is not null)
                errors.Add(ScanError);
        }

        ScanError = errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    /// <summary>
    /// Scans everything configured for both games in one pass: FM folders for missions not yet
    /// cataloged, and Downloads folders for archives not yet installed.
    /// </summary>
    public async Task RefreshAsync(AppSettings settings)
    {
        var errors = new List<string>();

        await ScanAllInstalled(settings);
        if (ScanError is not null)
            errors.Add(ScanError);

        await ScanAllDownloads(settings);
        if (ScanError is not null)
            errors.Add(ScanError);

        ScanError = errors.Count > 0 ? string.Join(" ", errors) : null;
    }

    private void RemoveCandidates(Func<ScanCandidateViewModel, bool> match)
    {
        foreach (var candidate in Candidates.Where(match).ToList())
            Candidates.Remove(candidate);
    }

    private async Task IgnoreCandidateAsync(ScanCandidateViewModel? candidate)
    {
        if (candidate is null)
            return;

        await _ignoredFmRepository.AddAsync(candidate.Game, candidate.SuggestedTitle);
        RemoveCandidates(c => c.Game == candidate.Game && FmNameMatcher.AreSimilar(c.SuggestedTitle, candidate.SuggestedTitle));
    }

    private async Task ImportSelectedAsync()
    {
        var selected = Candidates.Where(c => c.IsSelected).ToList();
        foreach (var candidate in selected)
        {
            await _missionRepository.AddAsync(new FanMission
            {
                Title = candidate.SuggestedTitle,
                Game = candidate.Game,
                FolderPath = candidate.FolderPath,
                ArchivePath = candidate.ArchivePath,
                InstallStatus = candidate.ArchivePath is null ? InstallStatus.Installed : InstallStatus.NotInstalled
            });
            Candidates.Remove(candidate);
        }
    }
}
