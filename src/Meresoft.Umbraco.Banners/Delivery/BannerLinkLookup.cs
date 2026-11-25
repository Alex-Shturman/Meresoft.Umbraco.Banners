using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <inheritdoc cref="IBannerLinkLookup" />
/// <remarks>
/// The database reads here are synchronous on purpose: every repository of the package works on the ambient scope and
/// is synchronous. Only a click reaches them (never a page or a seen request), and an id that cannot exist, an id that
/// was looked up lately and every banner of the live snapshot cost no read.
/// </remarks>
internal sealed class BannerLinkLookup : IBannerLinkLookup
{
    private const int DefaultMaxCachedBanners = 2_000;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly IScopeProvider _scopeProvider;
    private readonly IBannerRepository _banners;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxCachedBanners;
    private readonly ConcurrentDictionary<int, CachedLink> _cache = new();
    private volatile KnownMaxId? _knownMaxId;

    public BannerLinkLookup(
        ILiveSnapshotAccessor snapshot,
        IScopeProvider scopeProvider,
        IBannerRepository banners,
        TimeProvider timeProvider,
        int maxCachedBanners = DefaultMaxCachedBanners)
    {
        _snapshot = snapshot;
        _scopeProvider = scopeProvider;
        _banners = banners;
        _timeProvider = timeProvider;
        _maxCachedBanners = maxCachedBanners;
    }

    public ClickTarget? Find(int bannerId, int? zoneId)
    {
        if (zoneId is { } zone && FromSnapshot(bannerId, zone) is { } live)
        {
            return live;
        }

        // A random id from outside is most likely above every banner there is: no need to read for it.
        if (bannerId > MaxKnownBannerId())
        {
            return null;
        }

        LiveLink? link = FromCache(bannerId);
        return link is null
            ? null
            : new ClickTarget(link.LinkUrl, zoneId is { } z && link.IsPublished && link.LiveZoneIds.Contains(z));
    }

    private ClickTarget? FromSnapshot(int bannerId, int zoneId)
    {
        // A page request never gets here; a click is rare enough that scanning the few zones and candidates is fine.
        foreach (SnapshotZone zone in _snapshot.Current.Zones.Values)
        {
            if (zone.Id != zoneId)
            {
                continue;
            }

            SnapshotBanner? candidate = zone.Candidates.FirstOrDefault(c => c.BannerId == bannerId);
            return candidate is null || !LinkUrlRules.IsAbsoluteHttpUrl(candidate.LinkUrl) ? null : new ClickTarget(candidate.LinkUrl, Countable: true);
        }

        return null;
    }

    /// <summary>
    /// The highest id a banner can have: the larger of the live snapshot's and the database's. The database value is read
    /// again every 30 seconds and whenever the snapshot was rebuilt. A banner reaches a page only by being published, which rebuilds
    /// the snapshot, so a banner that was ever shown is never above the value that is used.
    /// </summary>
    private int MaxKnownBannerId()
    {
        int fromSnapshot = 0;
        foreach (SnapshotZone zone in _snapshot.Current.Zones.Values)
        {
            foreach (SnapshotBanner candidate in zone.Candidates)
            {
                fromSnapshot = Math.Max(fromSnapshot, candidate.BannerId);
            }
        }

        long now = _timeProvider.GetTimestamp();
        LiveSnapshot current = _snapshot.Current;
        KnownMaxId? known = _knownMaxId;
        if (known is null || known.Snapshot != current || _timeProvider.GetElapsedTime(known.At, now) >= CacheLifetime)
        {
            using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
            known = _knownMaxId = new KnownMaxId(_banners.GetMaxId(), now, current);
        }

        return Math.Max(fromSnapshot, known.Id);
    }

    private LiveLink? FromCache(int bannerId)
    {
        long now = _timeProvider.GetTimestamp();
        if (_cache.TryGetValue(bannerId, out CachedLink? cached) && _timeProvider.GetElapsedTime(cached.At, now) < CacheLifetime)
        {
            return cached.Link;
        }

        LiveLink? link = ReadFromDatabase(bannerId);
        if (_cache.Count >= _maxCachedBanners)
        {
            // Drop only what has expired. If the cache is still full of live entries, this one is simply not kept:
            // clearing everything would let a flood of unknown ids push the real banners out.
            foreach (KeyValuePair<int, CachedLink> entry in _cache)
            {
                if (_timeProvider.GetElapsedTime(entry.Value.At, now) >= CacheLifetime)
                {
                    _cache.TryRemove(entry);
                }
            }

            if (_cache.Count >= _maxCachedBanners)
            {
                return link;
            }
        }

        _cache[bannerId] = new CachedLink(link, now);
        return link;
    }

    private LiveLink? ReadFromDatabase(int bannerId)
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        Banner? banner = _banners.GetById(bannerId);
        if (banner is not { Type: BannerType.Image, Live.LinkUrl: { } url } || !LinkUrlRules.IsAbsoluteHttpUrl(url))
        {
            return null;
        }

        return new LiveLink(url, banner.IsPublished, [.. banner.Live.ZoneIds]);
    }

    private sealed record LiveLink(string LinkUrl, bool IsPublished, HashSet<int> LiveZoneIds);

    private sealed record CachedLink(LiveLink? Link, long At);

    private sealed record KnownMaxId(int Id, long At, LiveSnapshot Snapshot);
}
