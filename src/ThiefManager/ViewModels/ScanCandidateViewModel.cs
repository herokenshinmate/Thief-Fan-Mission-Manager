using CommunityToolkit.Mvvm.ComponentModel;

namespace ThiefManager.ViewModels;

public partial class ScanCandidateViewModel : ObservableObject
{
    public ScanCandidateViewModel(string suggestedTitle, string folderPath, string? archivePath = null)
    {
        SuggestedTitle = suggestedTitle;
        FolderPath = folderPath;
        ArchivePath = archivePath;
    }

    [ObservableProperty] private string suggestedTitle;
    [ObservableProperty] private string folderPath;
    [ObservableProperty] private bool isSelected;

    public string? ArchivePath { get; }
}
