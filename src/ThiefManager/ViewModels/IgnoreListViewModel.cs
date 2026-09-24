using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.ViewModels;

public partial class IgnoreListViewModel : ObservableObject
{
    private readonly IIgnoredFmRepository _ignoredFmRepository;

    public IgnoreListViewModel(IIgnoredFmRepository ignoredFmRepository)
    {
        _ignoredFmRepository = ignoredFmRepository;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        RemoveCommand = new AsyncRelayCommand<IgnoredFm>(RemoveAsync);
    }

    public ObservableCollection<IgnoredFm> Items { get; } = new();
    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand<IgnoredFm> RemoveCommand { get; }

    private async Task LoadAsync()
    {
        var all = await _ignoredFmRepository.GetAllAsync();
        Items.Clear();
        foreach (var item in all.OrderBy(i => i.Game).ThenBy(i => i.Name))
            Items.Add(item);
    }

    private async Task RemoveAsync(IgnoredFm? item)
    {
        if (item is null)
            return;

        await _ignoredFmRepository.DeleteAsync(item.Id);
        Items.Remove(item);
    }
}
