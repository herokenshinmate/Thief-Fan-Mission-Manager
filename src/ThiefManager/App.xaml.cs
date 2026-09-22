using System.IO;
using System.Windows;
using System.Windows.Media;
using ThiefManager.Data;
using ThiefManager.Services;
using ThiefManager.ViewModels;
using ThiefManager.Views;
using Wpf.Ui.Appearance;

namespace ThiefManager;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
        ApplicationAccentColorManager.Apply(Color.FromRgb(0xC9, 0xA2, 0x27));

        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ThiefManager");
        Directory.CreateDirectory(appDataFolder);
        var dbPath = Path.Combine(appDataFolder, "thiefmanager.db");

        ThiefManagerDbContext CreateContext() => new(dbPath);
        using (var db = CreateContext())
            db.Database.EnsureCreated();
        SchemaUpgrader.EnsureColumns(dbPath);

        var missionRepository = new MissionRepository(CreateContext);
        var settingsRepository = new SettingsRepository(CreateContext);
        var launchService = new LaunchService(new ProcessLauncher(), new FileExistsChecker());
        var directoryReader = new DirectoryReader();
        var archiveFileReader = new ArchiveFileReader();
        var archiveInstaller = new ArchiveInstaller();
        var folderDeleter = new FolderDeleter();
        var thiefGuildLookupService = new ThiefGuildLookupService();

        var mainViewModel = new MainViewModel(missionRepository, launchService, archiveInstaller, folderDeleter);
        var settings = await settingsRepository.GetAsync();

        var isFirstRun = string.IsNullOrWhiteSpace(settings.Thief1FmFolder)
            && string.IsNullOrWhiteSpace(settings.Thief2FmFolder)
            && string.IsNullOrWhiteSpace(settings.Thief1ExePath)
            && string.IsNullOrWhiteSpace(settings.Thief2ExePath);

        if (isFirstRun)
        {
            var firstRunSettingsViewModel = new SettingsViewModel(settingsRepository);
            await firstRunSettingsViewModel.LoadCommand.ExecuteAsync(null);
            var firstRunSettingsWindow = new SettingsWindow(firstRunSettingsViewModel);
            firstRunSettingsViewModel.Saved += (_, _) => firstRunSettingsWindow.Close();
            firstRunSettingsWindow.ShowDialog();

            settings = await settingsRepository.GetAsync();
        }

        mainViewModel.ConfigureExePaths(settings.Thief1ExePath, settings.Thief2ExePath);
        GameIconStore.UpdatePaths(settings.Thief1ExePath, settings.Thief2ExePath);

        var mainWindow = new MainWindow(mainViewModel, missionRepository, settingsRepository, launchService, directoryReader, archiveFileReader, thiefGuildLookupService);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }
}
