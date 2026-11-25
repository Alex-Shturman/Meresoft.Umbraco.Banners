using Meresoft.Umbraco.Banners.Caching;

namespace Meresoft.Umbraco.Banners.Selection;

/// <inheritdoc cref="IBannerSelector" />
internal sealed class BannerSelector : IBannerSelector
{
    private readonly ILiveSnapshotAccessor _snapshot;

    public BannerSelector(ILiveSnapshotAccessor snapshot) => _snapshot = snapshot;

    public IReadOnlyList<SelectedBanner> Select(string zoneAlias)
    {
        if (!_snapshot.Current.Zones.TryGetValue(zoneAlias, out SnapshotZone? zone) || zone.Candidates.Count == 0)
        {
            return [];
        }

        int count = Math.Min(zone.ShowCount, zone.Candidates.Count);
        return Pick(zone.Candidates, count).Select(ToSelected).ToArray();
    }

    // Uniform random choice of different candidates. US2 replaces this method with the weighted sampler.
    private static IEnumerable<SnapshotBanner> Pick(IReadOnlyList<SnapshotBanner> candidates, int count)
    {
        if (count == 1)
        {
            // The common case allocates no copy and shuffles nothing.
            return [candidates[Random.Shared.Next(candidates.Count)]];
        }

        SnapshotBanner[] pool = [.. candidates];
        Random.Shared.Shuffle(pool);
        return pool.Take(count);
    }

    private static SelectedBanner ToSelected(SnapshotBanner banner) => new()
    {
        BannerId = banner.BannerId,
        ZoneId = banner.ZoneId,
        ImageUrl = banner.ImageUrl,
        Width = banner.Width,
        Height = banner.Height,
        AltText = banner.AltText,
        Title = banner.Title,
        LinkUrl = banner.LinkUrl,
        OpenInNewWindow = banner.OpenInNewWindow,
    };
}
