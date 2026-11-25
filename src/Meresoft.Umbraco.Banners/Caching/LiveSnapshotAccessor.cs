using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Caching;

/// <inheritdoc cref="ILiveSnapshotAccessor" />
internal sealed class LiveSnapshotAccessor : ILiveSnapshotAccessor
{
    private readonly IScopeProvider _scopeProvider;
    private readonly IZoneRepository _zones;
    private readonly IBannerRepository _banners;
    private readonly LiveSnapshotBuilder _builder;
    private readonly ILogger<LiveSnapshotAccessor> _logger;

    // Rebuilds run one at a time so an older read can never replace a newer one.
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private volatile LiveSnapshot _current = LiveSnapshot.Empty;

    public LiveSnapshotAccessor(
        IScopeProvider scopeProvider,
        IZoneRepository zones,
        IBannerRepository banners,
        LiveSnapshotBuilder builder,
        ILogger<LiveSnapshotAccessor> logger)
    {
        _scopeProvider = scopeProvider;
        _zones = zones;
        _banners = banners;
        _builder = builder;
        _logger = logger;
    }

    public LiveSnapshot Current => _current;

    public async Task RebuildAsync()
    {
        await _rebuildLock.WaitAsync();
        try
        {
            IReadOnlyList<BannerZone> zones;
            IReadOnlyList<Banner> banners;
            using (IScope scope = _scopeProvider.CreateScope(autoComplete: true))
            {
                zones = _zones.GetAll();
                banners = _banners.GetAllPublished();
            }

            _current = await _builder.BuildAsync(zones, banners);
            _logger.LogDebug("Banner snapshot rebuilt: {ZoneCount} zones, {MissingImageCount} banners with a missing image.", zones.Count, _current.ImageMissingBannerIds.Count);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }
}
