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
    private int _id;
    private bool _thiefGuildLookupDismissed;

    public MissionEditViewModel(IMissionRepository missionRepository, IThiefGuildLookupService thiefGuildLookupService)
    {
        _missionRepository = missionRepository;
        _thiefGuildLookupService = thiefGuildLookupService;
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
        ThiefGuildLookupStatus = "Metadata updated from Thief Guild.";
    }

    private async Task SaveAsync()
    {
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
            ThiefGuildLookupDismissed = _thiefGuildLookupDismissed
        };

        MissionStatusDates.Apply(mission, Status, DateTime.Now);
        DateStarted = mission.DateStarted;
        DateCompleted = mission.DateCompleted;

        if (_id == 0)
            await _missionRepository.AddAsync(mission);
        else
            await _missionRepository.UpdateAsync(mission);

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
