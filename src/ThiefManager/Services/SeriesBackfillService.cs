using ThiefManager.Data;

namespace ThiefManager.Services;

/// <summary>
/// Fills in series info for missions linked to Thief Guild before series support existed. Runs
/// once per mission: a successful fetch marks it checked whether or not it's in a series, and a
/// failed one leaves it for the next launch. Requests are sequential and spaced out to go easy on
/// the site.
/// </summary>
public class SeriesBackfillService
{
    private static readonly TimeSpan DelayBetweenRequests = TimeSpan.FromSeconds(1);

    private readonly IMissionRepository _missionRepository;
    private readonly ISeriesRepository _seriesRepository;
    private readonly IThiefGuildLookupService _lookupService;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public SeriesBackfillService(
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

    /// <summary>Raised after a mission has been placed into a series.</summary>
    public event EventHandler? SeriesAssigned;

    public async Task RunAsync(IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var candidates = (await _missionRepository.GetAllAsync())
            .Where(m => !string.IsNullOrWhiteSpace(m.ThiefGuildUrl) && m.SeriesId is null && !m.SeriesLookupChecked)
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
                    await SeriesAssigner.ApplyAsync(mission, result.Series, _seriesRepository);
                    // Series columns only: `mission` was loaded before the loop and may be stale.
                    await _missionRepository.ApplySeriesLookupAsync(mission.Id, mission.SeriesId, mission.SeriesPosition);
                    if (mission.SeriesId is not null)
                        SeriesAssigned?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // A malformed URL or unexpected failure for this mission shouldn't stop the rest
                // of the backfill; it's left unchecked and retried on the next launch.
            }

            progress?.Report((i + 1, candidates.Count));
        }

        // A series created for a mission deleted mid-run would otherwise linger.
        await _seriesRepository.DeleteOrphansAsync();
    }
}
