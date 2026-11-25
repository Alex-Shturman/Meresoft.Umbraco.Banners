using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <inheritdoc cref="IBannerLinkLookup" />
internal sealed class BannerLinkLookup : IBannerLinkLookup
{
    private const int MaxCachedBanners = 2_000;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly IScopeProvider _scopeProvider;
    private readonly IBannerRepository _banners;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<int, CachedLink> _cache = new();

    public BannerLinkLookup(ILiveSnapshotAccessor snapshot, IScopeProvider scopeProvider, IBannerRepository banners, TimeProvider timeProvider)
    {
        _snapshot = snapshot;
        _scopeProvider = scopeProvider;
        _banners = banners;
        _timeProvider = timeProvider;
    }

    public ClickTarget? Find(int bannerId, int? zoneId)
    {
        if (zoneId is { } zone && FromSnapshot(bannerId, zone) is { } live)
        {
            return live;
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
            return candidate is null ? null : new ClickTarget(candidate.LinkUrl, Countable: true);
        }

        return null;
    }

    private LiveLink? FromCache(int bannerId)
    {
        long now = _timeProvider.GetTimestamp();
        if (_cache.TryGetValue(bannerId, out CachedLink? cached) && _timeProvider.GetElapsedTime(cached.At, now) < CacheLifetime)
        {
            return cached.Link;
        }

        LiveLink? link = ReadFromDatabase(bannerId);
        if (_cache.Count >= MaxCachedBanners)
        {
            // Unknown ids from outside can fill the cache; starting again keeps memory bounded.
            _cache.Clear();
        }

        _cache[bannerId] = new CachedLink(link, now);
        return link;
    }

    private LiveLink? ReadFromDatabase(int bannerId)
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        Banner? banner = _banners.GetById(bannerId);
        if (banner is not { Type: BannerType.Image, Live.LinkUrl: { } url }
            || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return new LiveLink(url, banner.IsPublished, [.. banner.Live.ZoneIds]);
    }

    private sealed record LiveLink(string LinkUrl, bool IsPublished, HashSet<int> LiveZoneIds);

    private sealed record CachedLink(LiveLink? Link, long At);
}
