using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class MissionEditViewModel : ObservableObject
{
    private readonly IMissionRepository _missionRepository;
    private int _id;

    public MissionEditViewModel(IMissionRepository missionRepository)
    {
        _missionRepository = missionRepository;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public event EventHandler? Saved;

    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private GameTitle game;
    [ObservableProperty] private string? author;
    [ObservableProperty] private int? releaseYear;
    [ObservableProperty] private MissionStatus status = MissionStatus.NotPlayed;
    [ObservableProperty] private int? rating;
    [ObservableProperty] private string tags = string.Empty;
    [ObservableProperty] private string? notes;
    [ObservableProperty] private DateTime? dateStarted;
    [ObservableProperty] private DateTime? dateCompleted;
    [ObservableProperty] private string folderPath = string.Empty;

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
            Status = Status,
            Rating = Rating,
            Tags = Tags,
            Notes = Notes,
            DateStarted = DateStarted,
            DateCompleted = DateCompleted,
            FolderPath = FolderPath
        };

        if (_id == 0)
            await _missionRepository.AddAsync(mission);
        else
            await _missionRepository.UpdateAsync(mission);

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
