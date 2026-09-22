using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public async Task LoadCommand_PopulatesPropertiesFromRepository()
    {
        var repo = new FakeSettingsRepository();
        await repo.SaveAsync(new ThiefManager.Models.AppSettings { Thief1ExePath = @"C:\Thief.exe" });
        var vm = new SettingsViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Thief.exe", vm.Thief1ExePath);
    }

    [Fact]
    public async Task SaveCommand_PersistsPropertiesAndRaisesSaved()
    {
        var repo = new FakeSettingsRepository();
        var vm = new SettingsViewModel(repo)
        {
            Thief1FmFolder = @"C:\fms1",
            Thief2FmFolder = @"C:\fms2",
            Thief1ExePath = @"C:\Thief.exe",
            Thief2ExePath = @"C:\Thief2.exe",
            Thief1DownloadsFolder = @"C:\Downloads1",
            Thief2DownloadsFolder = @"C:\Downloads2"
        };
        var raised = false;
        vm.Saved += (_, _) => raised = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(raised);
        var saved = await repo.GetAsync();
        Assert.Equal(@"C:\fms1", saved.Thief1FmFolder);
        Assert.Equal(@"C:\Thief2.exe", saved.Thief2ExePath);
        Assert.Equal(@"C:\Downloads1", saved.Thief1DownloadsFolder);
        Assert.Equal(@"C:\Downloads2", saved.Thief2DownloadsFolder);
    }
}
