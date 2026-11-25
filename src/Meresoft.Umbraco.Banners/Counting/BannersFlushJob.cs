using System.Data.Common;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>
/// Writes the in-memory work of the last interval to the database, off the request path (FR-012).
/// That is the counted impressions and clicks (<see cref="CounterFlusher"/>) and the requested zones.
/// Runs on every server, because every server counts its own page requests.
/// </summary>
internal sealed class BannersFlushJob : RecurringBackgroundJobBase
{
    private static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(30);

    private readonly RequestedZoneRecorder _recorder;
    private readonly CounterFlusher _counters;
    private readonly IScopeProvider _scopeProvider;
    private readonly IRequestedZoneRepository _requestedZones;
    private readonly IZoneRepository _zones;
    private readonly ILogger<BannersFlushJob> _logger;

    public BannersFlushJob(
        IOptions<BannersOptions> options,
        RequestedZoneRecorder recorder,
        CounterFlusher counters,
        IScopeProvider scopeProvider,
        IRequestedZoneRepository requestedZones,
        IZoneRepository zones,
        ILogger<BannersFlushJob> logger)
        : base(options.Value.FlushInterval)
    {
        _recorder = recorder;
        _counters = counters;
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
        _counters.Flush();

        IReadOnlyList<RequestedZone> requested = _recorder.Drain();
        if (requested.Count == 0)
        {
            return Task.CompletedTask;
        }

        // One scope per alias: a failure on one alias (for example two servers inserting the same new alias
        // at once) must not roll back or lose the others.
        foreach (RequestedZone item in requested)
        {
            try
            {
                WriteOne(item);
            }
            catch (DbException ex)
            {
                // A unique index violation from a racing server: the row exists now, so the second try is an update.
                _logger.LogDebug(ex, "Writing requested banner zone {Alias} failed; trying once more.", item.Alias);
                try
                {
                    WriteOne(item);
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Writing requested banner zone {Alias} failed twice; it is asked for again on the next page request.", item.Alias);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Writing requested banner zone {Alias} failed; it is asked for again on the next page request.", item.Alias);
            }
        }

        return Task.CompletedTask;
    }

    private void WriteOne(RequestedZone item)
    {
        // The check is its own short scope, so no read lock is held while the write waits for its turn.
        bool zoneExists;
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            // A zone with this alias may have been created since the template asked for it.
            zoneExists = _zones.GetByAlias(item.Alias) is not null;
        }

        if (zoneExists)
        {
            return;
        }

        using IScope scope = _scopeProvider.CreateScope();
        scope.TakeWriteLock();
        _requestedZones.Upsert(item.Alias, item.FirstSeen, item.LastSeen);
        scope.Complete();
    }
}
