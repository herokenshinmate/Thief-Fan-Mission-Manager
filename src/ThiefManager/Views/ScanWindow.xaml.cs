using System.Windows;
using ThiefManager.Models;
using ThiefManager.ViewModels;

namespace ThiefManager.Views;

public partial class ScanWindow : Window
{
    public ScanWindow(ScanViewModel viewModel, AppSettings settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(settings.Thief1FmFolder))
                await viewModel.Scan(GameTitle.Thief1, settings.Thief1FmFolder);
            if (!string.IsNullOrWhiteSpace(settings.Thief2FmFolder))
                await viewModel.Scan(GameTitle.Thief2, settings.Thief2FmFolder);
        };
    }
}
