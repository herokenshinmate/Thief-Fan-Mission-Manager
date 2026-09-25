using ThiefManager.Data;
using ThiefManager.Models;

namespace ThiefManager.Services;

/// <summary>
/// Fetches Thief Guild data for linked missions: at startup only for missions last fetched by an
/// older parser (below ThiefGuildMetadata.CurrentVersion), or for every linked mission when the
/// user asks to refresh. A successful fetch stamps the current version; a failed one leaves it so
/// the mission is retried next launch. Requests are sequential and spaced out to go easy on the site.
/// </summary>
public class ThiefGuildBackfillService
{
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(1);

    private readonly IMissionRepository _missionRepository;
    private readonly ISeriesRepository _seriesRepository;
    private readonly IThiefGuildLookupService _lookupService;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ThiefGuildBackfillService(
        IMissionRepository missionRepository,
        ISeriesRepository seriesRepository,
        IThiefGuildLookupService lookupService,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _missionRepository = missionRepository;
        _seriesRepository = seriesRepository;
        _lookupService = lookupService;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>Raised after each mission whose fetched data was saved, with the mission just fetched.</summary>
    public event EventHandler<FanMission>? MissionUpdated;

    public async Task RunAsync(IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken, bool refreshAll = false)
    {
        var candidates = (await _missionRepository.GetAllAsync())
            .Where(m => !string.IsNullOrWhiteSpace(m.ThiefGuildUrl)
                && (refreshAll || m.ThiefGuildMetadataVersion < ThiefGuildMetadata.CurrentVersion))
            .ToList();
        if (candidates.Count == 0)
            return;

        progress?.Report((0, candidates.Count));
        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (i > 0)
                await _delay(DelayBetweenRequests, cancellationToken);

            var mission = candidates[i];
            try
            {
                var result = await _lookupService.FetchByUrlAsync(mission.ThiefGuildUrl!);
                if (result is not null)
                {
                    ThiefGuildMetadataApplier.Apply(mission, result);
                    await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
                    // Fetch-owned columns only: `mission` was loaded before the loop and may be stale.
                    await _missionRepository.ApplyThiefGuildMetadataAsync(mission);
                    MissionUpdated?.Invoke(this, mission);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A malformed URL or unexpected failure for this mission shouldn't stop the rest;
                // its version is left as-is, so it's retried on the next launch.
            }

            progress?.Report((i + 1, candidates.Count));
        }

        // A series created for a mission deleted mid-run would otherwise linger.
        await _seriesRepository.DeleteOrphansAsync();
    }
}
