using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;

    public SettingsViewModel(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public event EventHandler? Saved;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }

    [ObservableProperty] private string? thief1FmFolder;
    [ObservableProperty] private string? thief2FmFolder;
    [ObservableProperty] private string? thief1ExePath;
    [ObservableProperty] private string? thief2ExePath;
    [ObservableProperty] private string? thief1DownloadsFolder;
    [ObservableProperty] private string? thief2DownloadsFolder;

    private async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        Thief1FmFolder = settings.Thief1FmFolder;
        Thief2FmFolder = settings.Thief2FmFolder;
        Thief1ExePath = settings.Thief1ExePath;
        Thief2ExePath = settings.Thief2ExePath;
        Thief1DownloadsFolder = settings.Thief1DownloadsFolder;
        Thief2DownloadsFolder = settings.Thief2DownloadsFolder;
    }

    private async Task SaveAsync()
    {
        await _settingsRepository.SaveAsync(new AppSettings
        {
            Thief1FmFolder = Thief1FmFolder,
            Thief2FmFolder = Thief2FmFolder,
            Thief1ExePath = Thief1ExePath,
            Thief2ExePath = Thief2ExePath,
            Thief1DownloadsFolder = Thief1DownloadsFolder,
            Thief2DownloadsFolder = Thief2DownloadsFolder
        });

        Saved?.Invoke(this, EventArgs.Empty);
    }
}
