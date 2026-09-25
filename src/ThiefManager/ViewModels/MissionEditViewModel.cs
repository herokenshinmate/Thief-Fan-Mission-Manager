using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;
using ThiefManager.Services;

namespace ThiefManager.ViewModels;

public partial class MissionEditViewModel : ObservableObject
{
    private readonly IMissionRepository _missionRepository;
    private readonly IThiefGuildLookupService _thiefGuildLookupService;
    private readonly ISeriesRepository _seriesRepository;
    private int _id;
    private bool _thiefGuildLookupDismissed;
    private int? _seriesId;
    private string? _originalSeriesName;
    private int? _originalSeriesPosition;
    private bool _seriesLookupChecked;
    private ThiefGuildSeriesInfo? _fetchedSeries;

    public MissionEditViewModel(IMissionRepository missionRepository, IThiefGuildLookupService thiefGuildLookupService, ISeriesRepository seriesRepository)
    {
        _missionRepository = missionRepository;
        _thiefGuildLookupService = thiefGuildLookupService;
        _seriesRepository = seriesRepository;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        FetchThiefGuildMetadataCommand = new AsyncRelayCommand(FetchThiefGuildMetadataAsync);
    }

    public event EventHandler? Saved;

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand FetchThiefGuildMetadataCommand { get; }

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private GameTitle game;
    [ObservableProperty] private string? author;
    [ObservableProperty] private int? releaseYear;
    [ObservableProperty] private MissionStatus status = MissionStatus.NotPlayed;

    public string[] RatingOptions { get; } = { "(Not Rated)", "0", "1", "2", "3", "4", "5" };

    public string RatingDisplay
    {
        get => Rating?.ToString() ?? "(Not Rated)";
        set => Rating = value == "(Not Rated)" ? null : int.Parse(value);
    }

    [ObservableProperty] private int? rating;

    partial void OnRatingChanged(int? value) => OnPropertyChanged(nameof(RatingDisplay));
    [ObservableProperty] private string tags = string.Empty;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private DateTime? dateStarted;
    [ObservableProperty] private DateTime? dateCompleted;
    [ObservableProperty] private string folderPath = string.Empty;
    [ObservableProperty] private string? thiefGuildUrl;
    [ObservableProperty] private string? thiefGuildLookupStatus;
    [ObservableProperty] private string? seriesName;
    [ObservableProperty] private int? seriesPosition;

    public ObservableCollection<string> SeriesNameOptions { get; } = new();

    public bool HasSeriesName => !string.IsNullOrWhiteSpace(SeriesName);

    partial void OnSeriesNameChanged(string? value) => OnPropertyChanged(nameof(HasSeriesName));

    /// <summary>
    /// Fills the Series dropdown and shows the mission's current series name. Call after LoadFrom.
    /// </summary>
    public async Task LoadSeriesOptionsAsync()
    {
        var all = await _seriesRepository.GetAllAsync();
        SeriesNameOptions.Clear();
        foreach (var name in all.Select(s => s.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            SeriesNameOptions.Add(name);

        _originalSeriesName = all.FirstOrDefault(s => s.Id == _seriesId)?.Name;
        SeriesName = _originalSeriesName;
    }

    public void LoadFrom(FanMission mission)
    {
        _id = mission.Id;
        Title = mission.Title;
        Game = mission.Game;
        Author = mission.Author;
        ReleaseYear = mission.ReleaseYear;
        Status = mission.Status;
        Rating = mission.Rating;
        Tags = mission.Tags;
        Notes = mission.Notes;
        DateStarted = mission.DateStarted;
        DateCompleted = mission.DateCompleted;
        FolderPath = mission.FolderPath;
        ThiefGuildUrl = mission.ThiefGuildUrl;
        _thiefGuildLookupDismissed = mission.ThiefGuildLookupDismissed;
        _seriesId = mission.SeriesId;
        SeriesPosition = mission.SeriesPosition;
        _originalSeriesPosition = mission.SeriesPosition;
        _seriesLookupChecked = mission.SeriesLookupChecked;
    }

    private async Task FetchThiefGuildMetadataAsync()
    {
        ThiefGuildLookupStatus = "Looking up...";

        var result = string.IsNullOrWhiteSpace(ThiefGuildUrl)
            ? await _thiefGuildLookupService.SearchByTitleAsync(Title)
            : await _thiefGuildLookupService.FetchByUrlAsync(ThiefGuildUrl);

        if (result is null)
        {
            ThiefGuildLookupStatus = "Couldn't find this mission on Thief Guild.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Author))
            Author = result.Author;
        if (result.ReleaseYear is not null)
            ReleaseYear = result.ReleaseYear;
        if (!string.IsNullOrWhiteSpace(result.Tags))
            Tags = result.Tags;
        ThiefGuildUrl = result.Url;
        _thiefGuildLookupDismissed = false;
        _seriesLookupChecked = true;
        if (result.Series is not null && string.IsNullOrWhiteSpace(SeriesName))
        {
            _fetchedSeries = result.Series;
            SeriesName = result.Series.Name;
            SeriesPosition = result.Series.Position;
        }
        ThiefGuildLookupStatus = "Metadata updated from Thief Guild.";
    }

    private async Task SaveAsync()
    {
        var trimmedSeriesName = string.IsNullOrWhiteSpace(SeriesName) ? null : SeriesName.Trim();
        var seriesNameChanged = !string.Equals(trimmedSeriesName, _originalSeriesName, StringComparison.Ordinal);

        // An untouched name keeps the existing link, even if the options were never loaded.
        var seriesId = seriesNameChanged
            ? trimmedSeriesName is null ? null : (await ResolveSeriesAsync(trimmedSeriesName)).Id
            : _seriesId;
        var seriesPosition = seriesId is null ? null : SeriesPosition;
        var seriesEdited = seriesNameChanged || seriesPosition != _originalSeriesPosition;

        var mission = new FanMission
        {
            Id = _id,
            Title = Title,
            Game = Game,
            Author = Author,
            ReleaseYear = ReleaseYear,
            Rating = Rating,
            Tags = Tags,
            Notes = Notes,
            DateStarted = DateStarted,
            DateCompleted = DateCompleted,
            FolderPath = FolderPath,
            ThiefGuildUrl = ThiefGuildUrl,
            ThiefGuildLookupDismissed = _thiefGuildLookupDismissed,
            SeriesId = seriesId,
            SeriesPosition = seriesPosition,
            SeriesLookupChecked = _seriesLookupChecked || seriesEdited
        };

        MissionStatusDates.Apply(mission, Status, DateTime.Now);
        DateStarted = mission.DateStarted;
        DateCompleted = mission.DateCompleted;

        if (_id == 0)
            await _missionRepository.AddAsync(mission);
        else
            await _missionRepository.UpdateAsync(mission);

        await _seriesRepository.DeleteOrphansAsync();

        Saved?.Invoke(this, EventArgs.Empty);
    }

    private Task<Series> ResolveSeriesAsync(string name) =>
        _fetchedSeries is not null && string.Equals(name, _fetchedSeries.Name, StringComparison.OrdinalIgnoreCase)
            ? _seriesRepository.GetOrCreateByThiefGuildIdAsync(_fetchedSeries.ThiefGuildSeriesId, _fetchedSeries.Name)
            : _seriesRepository.GetOrCreateByNameAsync(name);
}
