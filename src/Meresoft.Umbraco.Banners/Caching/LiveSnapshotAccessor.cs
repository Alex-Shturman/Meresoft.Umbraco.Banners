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
    private volatile bool _built;
    private int _warnedBeforeFirstBuild;

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

    public LiveSnapshot Current
    {
        get
        {
            // Before the first build every zone looks "not set up" and no banner shows. That is worth one line in
            // the log, because the cause (the build failed at start up) is otherwise invisible on the page.
            if (!IsBuilt && Interlocked.CompareExchange(ref _warnedBeforeFirstBuild, 1, 0) == 0)
            {
                _logger.LogWarning("The banner snapshot is read before its first build has succeeded; no banner is shown until it is built.");
            }

            return _current;
        }
    }

    public bool IsBuilt => _built;

    public async Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null)
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

            _current = await _builder.BuildAsync(zones, banners, mediaKeysTreatedAsMissing);
            _built = true;
            _logger.LogDebug("Banner snapshot rebuilt: {ZoneCount} zones, {MissingImageCount} banners with a missing image.", zones.Count, _current.ImageMissingBannerIds.Count);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }
}
