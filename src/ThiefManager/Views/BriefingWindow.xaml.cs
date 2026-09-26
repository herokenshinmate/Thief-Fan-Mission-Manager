using System.Windows;
using ThiefManager.Models;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class BriefingWindow : FluentWindow
{
    public string? VersionWarning { get; }

    public BriefingWindow(FanMission mission, string? versionWarning = null)
    {
        VersionWarning = versionWarning;
        InitializeComponent();
        DataContext = mission;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
