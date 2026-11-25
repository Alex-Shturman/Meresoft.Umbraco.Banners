using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>
/// Writes the in-memory work of the last interval to the database, off the request path (FR-012).
/// For now that is the requested zones; counts of impressions and clicks come with the statistics.
/// Runs on every server, because every server counts its own page requests.
/// </summary>
internal sealed class BannersFlushJob : RecurringBackgroundJobBase
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private readonly RequestedZoneRecorder _recorder;
    private readonly IScopeProvider _scopeProvider;
    private readonly IRequestedZoneRepository _requestedZones;
    private readonly IZoneRepository _zones;
    private readonly ILogger<BannersFlushJob> _logger;

    public BannersFlushJob(
        IOptions<BannersOptions> options,
        RequestedZoneRecorder recorder,
        IScopeProvider scopeProvider,
        IRequestedZoneRepository requestedZones,
        IZoneRepository zones,
        ILogger<BannersFlushJob> logger)
        : base(options.Value.FlushInterval)
    {
        _recorder = recorder;
        _scopeProvider = scopeProvider;
        _requestedZones = requestedZones;
        _zones = zones;
        _logger = logger;
    }

    /// <inheritdoc />
    public override TimeSpan Delay => StartDelay;

    /// <inheritdoc />
    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override Task RunJobAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RequestedZone> requested = _recorder.Drain();
        if (requested.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            using IScope scope = _scopeProvider.CreateScope();
            foreach (RequestedZone item in requested)
            {
                // A zone with this alias may have been created since the template asked for it.
                if (_zones.GetByAlias(item.Alias) is null)
                {
                    _requestedZones.Upsert(item.Alias, item.FirstSeen, item.LastSeen);
                }
            }

            scope.Complete();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Writing {Count} requested banner zones failed; they are asked for again on the next page request.", requested.Count);
        }

        return Task.CompletedTask;
    }
}
