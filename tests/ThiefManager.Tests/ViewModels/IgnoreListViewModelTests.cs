using ThiefManager.Models;
using ThiefManager.Tests.Fakes;
using ThiefManager.ViewModels;
using Xunit;

namespace ThiefManager.Tests.ViewModels;

public class IgnoreListViewModelTests
{
    [Fact]
    public async Task LoadCommand_PopulatesItemsSortedByGameThenName()
    {
        var repo = new FakeIgnoredFmRepository();
        await repo.AddAsync(GameTitle.Thief2, "Zeta");
        await repo.AddAsync(GameTitle.Thief1, "Beta");
        await repo.AddAsync(GameTitle.Thief1, "Alpha");
        var vm = new IgnoreListViewModel(repo);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Alpha", "Beta", "Zeta" }, vm.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task AddAsync_DoesNotDuplicateAnAlreadyIgnoredSimilarName()
    {
        var repo = new FakeIgnoredFmRepository();
        await repo.AddAsync(GameTitle.Thief1, "A New Job");

        await repo.AddAsync(GameTitle.Thief1, "A_New_Job_v2");

        Assert.Single(repo.IgnoredFms);
    }

    [Fact]
    public async Task RemoveCommand_DeletesFromRepositoryAndItems()
    {
        var repo = new FakeIgnoredFmRepository();
        await repo.AddAsync(GameTitle.Thief1, "Alpha");
        var vm = new IgnoreListViewModel(repo);
        await vm.LoadCommand.ExecuteAsync(null);
        var item = vm.Items.Single();

        await vm.RemoveCommand.ExecuteAsync(item);

        Assert.Empty(vm.Items);
        Assert.Empty(repo.IgnoredFms);
    }
}
