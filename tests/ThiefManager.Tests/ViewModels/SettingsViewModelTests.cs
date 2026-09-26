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
        await repo.SaveAsync(new ThiefManager.Models.AppSettings { Thief1ExePath = @"C:\Thief.exe", Thief1NewDarkVersion = "1.27" });
        var vm = new SettingsViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(@"C:\Thief.exe", vm.Thief1ExePath);
        Assert.Equal("1.27", vm.Thief1NewDarkVersion);
    }

    [Fact]
    public async Task LoadCommand_DefaultsShowMissionBriefingToTrueForANewSettingsRow()
    {
        var vm = new SettingsViewModel(new FakeSettingsRepository());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.True(vm.ShowMissionBriefing);
        Assert.True(vm.DoubleClickLaunchesPlay);
        Assert.True(vm.WarnOnNewDarkVersionMismatch);
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
            Thief2DownloadsFolder = @"C:\Downloads2",
            Thief1NewDarkVersion = "1.27",
            Thief2NewDarkVersion = "1.26",
            ShowMissionBriefing = false,
            DoubleClickLaunchesPlay = false,
            WarnOnNewDarkVersionMismatch = false
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
        Assert.Equal("1.27", saved.Thief1NewDarkVersion);
        Assert.Equal("1.26", saved.Thief2NewDarkVersion);
        Assert.False(saved.ShowMissionBriefing);
        Assert.False(saved.DoubleClickLaunchesPlay);
        Assert.False(saved.WarnOnNewDarkVersionMismatch);
    }

    [Fact]
    public void SettingThief1ExePath_UpdatesDetectedNewDarkVersion()
    {
        var vm = new SettingsViewModel(new FakeSettingsRepository())
        {
            Thief1ExePath = @"C:\Windows\System32\notepad.exe"
        };

        Assert.False(string.IsNullOrWhiteSpace(vm.Thief1DetectedNewDarkVersion));
    }

    [Fact]
    public void SettingThief1ExePathToAMissingFile_ClearsDetectedNewDarkVersion()
    {
        var vm = new SettingsViewModel(new FakeSettingsRepository())
        {
            Thief1ExePath = @"C:\does\not\exist.exe"
        };

        Assert.Null(vm.Thief1DetectedNewDarkVersion);
    }
}
