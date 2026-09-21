using System.IO;
using System.Windows;
using ThiefManager.Data;
using ThiefManager.Services;
using ThiefManager.ViewModels;

namespace ThiefManager;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThiefManager");
        Directory.CreateDirectory(appDataFolder);
        var dbPath = Path.Combine(appDataFolder, "thiefmanager.db");

        ThiefManagerDbContext CreateContext() => new(dbPath);
        using (var db = CreateContext())
            db.Database.EnsureCreated();

        var missionRepository = new MissionRepository(CreateContext);
        var settingsRepository = new SettingsRepository(CreateContext);
        var launchService = new LaunchService(new ProcessLauncher(), new FileExistsChecker());
        var directoryReader = new DirectoryReader();

        var mainViewModel = new MainViewModel(missionRepository, launchService);
        var settings = await settingsRepository.GetAsync();
        mainViewModel.ConfigureExePaths(settings.Thief1ExePath, settings.Thief2ExePath);

        var mainWindow = new MainWindow(mainViewModel, missionRepository, settingsRepository, launchService, directoryReader);
        mainWindow.Show();
    }
}
