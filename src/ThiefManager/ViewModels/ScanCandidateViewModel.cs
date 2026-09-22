using CommunityToolkit.Mvvm.ComponentModel;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class ScanCandidateViewModel : ObservableObject
{
    public ScanCandidateViewModel(GameTitle game, string suggestedTitle, string folderPath, string? archivePath = null)
    {
        Game = game;
        SuggestedTitle = suggestedTitle;
        FolderPath = folderPath;
        ArchivePath = archivePath;
    }

    public GameTitle Game { get; }

    [ObservableProperty] private string suggestedTitle;
    [ObservableProperty] private string folderPath;
    [ObservableProperty] private bool isSelected;

    public string? ArchivePath { get; }
}
