using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ThiefManager.Models;
using ThiefManager.ViewModels;
using Wpf.Ui.Controls;

namespace ThiefManager.Views;

public partial class ScanWindow : FluentWindow
{
    private readonly ScanViewModel _viewModel;
    private readonly AppSettings _settings;

    public ScanWindow(ScanViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        var candidatesView = CollectionViewSource.GetDefaultView(viewModel.Candidates);
        candidatesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ScanCandidateViewModel.GroupKey)));
        candidatesView.SortDescriptions.Add(new SortDescription(nameof(ScanCandidateViewModel.Game), ListSortDirection.Ascending));
        candidatesView.SortDescriptions.Add(new SortDescription(nameof(ScanCandidateViewModel.IsDownload), ListSortDirection.Ascending));
        CandidatesListView.ItemsSource = candidatesView;

        Loaded += async (_, _) => await _viewModel.RefreshAsync(_settings);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.RefreshAsync(_settings);

    private void CandidatesListView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<System.Windows.Controls.ListViewItem>(source) is { } item)
            item.IsSelected = true;
    }

    private async void IgnoreCandidate_Click(object sender, RoutedEventArgs e)
    {
        if (CandidatesListView.SelectedItem is ScanCandidateViewModel candidate)
            await _viewModel.IgnoreCandidateCommand.ExecuteAsync(candidate);
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
