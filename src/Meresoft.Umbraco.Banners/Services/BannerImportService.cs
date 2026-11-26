using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Persistence;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Services;

/// <inheritdoc cref="IBannerImportService" />
internal sealed class BannerImportService : IBannerImportService
{
    /// <summary>Rows per transaction, so a large history neither holds the write lock for long nor fills the transaction log.</summary>
    private const int BatchSize = 1_000;

    private readonly IScopeProvider _scopeProvider;
    private readonly IStatisticsRepository _statistics;
    private readonly IBannerRepository _banners;
    private readonly IZoneRepository _zones;
    private readonly ILiveSnapshotAccessor _snapshot;

    public BannerImportService(
        IScopeProvider scopeProvider,
        IStatisticsRepository statistics,
        IBannerRepository banners,
        IZoneRepository zones,
        ILiveSnapshotAccessor snapshot)
    {
        _scopeProvider = scopeProvider;
        _statistics = statistics;
        _banners = banners;
        _zones = zones;
        _snapshot = snapshot;
    }

    public async Task<BannerImportResult> ImportStatisticsAsync(IEnumerable<DailyStatisticImport> rows, StatisticImportMode mode)
    {
        ArgumentNullException.ThrowIfNull(rows);

        int inserted = 0;
        int updated = 0;
        int rejected = 0;
        bool replace = mode == StatisticImportMode.Replace;

        // Keys are resolved once per import, not once per row. Null means the key is unknown.
        var bannerIds = new Dictionary<Guid, int?>();
        var zoneIds = new Dictionary<Guid, int?>();

        foreach (DailyStatisticImport[] batch in rows.Chunk(BatchSize))
        {
            using IScope scope = _scopeProvider.CreateScope();
            scope.TakeWriteLock();
            foreach (DailyStatisticImport row in batch)
            {
                int? bannerId = null;
                int? zoneId = null;
                if (row is null || row.Impressions < 0 || row.Clicks < 0
                    || (bannerId = Resolve(bannerIds, row.BannerKey, key => _banners.GetByKey(key)?.Id)) is null
                    || (zoneId = Resolve(zoneIds, row.ZoneKey, key => _zones.GetByKey(key)?.Id)) is null)
                {
                    rejected++;
                    continue;
                }

                if (_statistics.Import(row.Day, bannerId.Value, zoneId.Value, row.Impressions, row.Clicks, replace))
                {
                    inserted++;
                }
                else
                {
                    updated++;
                }
            }

            scope.Complete();
        }

        // No scope is held from here on. Limit reloads only remove banners, so a Replace (totals can go down) rebuilds
        // everything to bring banners back; any other import that wrote rows reloads the limits, so a banner that the
        // imported counts pushed over its limit leaves the site at once instead of after the next flush.
        if (inserted + updated > 0)
        {
            if (replace)
            {
                await _snapshot.RebuildAsync();
            }
            else
            {
                await _snapshot.ReloadLimitsAsync();
            }
        }

        return new BannerImportResult(inserted, updated, rejected);
    }

    private static int? Resolve(Dictionary<Guid, int?> cache, Guid key, Func<Guid, int?> lookup)
    {
        if (!cache.TryGetValue(key, out int? id))
        {
            id = lookup(key);
            cache[key] = id;
        }

        return id;
    }
}
