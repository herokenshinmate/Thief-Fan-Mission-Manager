using CommunityToolkit.Mvvm.ComponentModel;

namespace ThiefManager.ViewModels;

public partial class ScanCandidateViewModel : ObservableObject
{
    public ScanCandidateViewModel(string suggestedTitle, string folderPath)
    {
        SuggestedTitle = suggestedTitle;
        FolderPath = folderPath;
    }

    [ObservableProperty] private string suggestedTitle;
    [ObservableProperty] private string folderPath;
    [ObservableProperty] private bool isSelected;
}
