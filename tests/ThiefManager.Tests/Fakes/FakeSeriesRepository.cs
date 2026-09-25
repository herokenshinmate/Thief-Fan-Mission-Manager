using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Tests.Fakes;

public class FakeSeriesRepository : ISeriesRepository
{
    private readonly FakeMissionRepository _missions;

    public FakeSeriesRepository(FakeMissionRepository missions) => _missions = missions;

    public List<Series> SeriesList { get; } = new();
    public List<SeriesPart> PartsList { get; } = new();

    public Task<List<Series>> GetAllAsync() => Task.FromResult(SeriesList.ToList());

    public Task<Series> GetOrCreateByThiefGuildIdAsync(int thiefGuildSeriesId, string name)
    {
        var trimmedName = name.Trim();
        var existing = SeriesList.FirstOrDefault(s => s.ThiefGuildSeriesId == thiefGuildSeriesId)
            ?? SeriesList.FirstOrDefault(s => s.ThiefGuildSeriesId is null
                && string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            // Don't overwrite a name the user may have set; only a newly created series takes
            // the name from Thief Guild.
            existing.ThiefGuildSeriesId = thiefGuildSeriesId;
            return Task.FromResult(existing);
        }

        var created = new Series { Id = NextId(), Name = trimmedName, ThiefGuildSeriesId = thiefGuildSeriesId };
        SeriesList.Add(created);
        return Task.FromResult(created);
    }

    public Task<Series> GetOrCreateByNameAsync(string name)
    {
        var trimmedName = name.Trim();
        var existing = SeriesList.FirstOrDefault(s => string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return Task.FromResult(existing);

        var created = new Series { Id = NextId(), Name = trimmedName };
        SeriesList.Add(created);
        return Task.FromResult(created);
    }

    public Task RenameAsync(int id, string name)
    {
        var series = SeriesList.FirstOrDefault(s => s.Id == id);
        if (series is not null && !string.IsNullOrWhiteSpace(name))
            series.Name = name.Trim();
        return Task.CompletedTask;
    }

    public Task SetExpandedAsync(int id, bool isExpanded)
    {
        var series = SeriesList.FirstOrDefault(s => s.Id == id);
        if (series is not null)
            series.IsExpanded = isExpanded;
        return Task.CompletedTask;
    }

    public Task<List<SeriesPart>> GetAllPartsAsync() => Task.FromResult(PartsList.ToList());

    public Task ReplacePartsAsync(int seriesId, IReadOnlyList<SeriesPart> parts)
    {
        PartsList.RemoveAll(p => p.SeriesId == seriesId);
        var nextId = PartsList.Count == 0 ? 1 : PartsList.Max(p => p.Id) + 1;
        foreach (var part in parts)
            PartsList.Add(new SeriesPart { Id = nextId++, SeriesId = seriesId, Position = part.Position, Title = part.Title, ThiefGuildUrl = part.ThiefGuildUrl });
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        foreach (var mission in _missions.Missions.Where(m => m.SeriesId == id))
        {
            mission.SeriesId = null;
            mission.SeriesPosition = null;
            mission.SeriesLookupChecked = true;
        }
        PartsList.RemoveAll(p => p.SeriesId == id);
        SeriesList.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }

    public Task DeleteOrphansAsync()
    {
        SeriesList.RemoveAll(s => !_missions.Missions.Any(m => m.SeriesId == s.Id));
        PartsList.RemoveAll(p => SeriesList.All(s => s.Id != p.SeriesId));
        return Task.CompletedTask;
    }

    private int NextId() => SeriesList.Count == 0 ? 1 : SeriesList.Max(s => s.Id) + 1;
}
