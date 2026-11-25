using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Selection;

/// <inheritdoc cref="IBannerSelector" />
internal sealed class BannerSelector : IBannerSelector
{
    private readonly ILiveSnapshotAccessor _snapshot;

    public BannerSelector(ILiveSnapshotAccessor snapshot) => _snapshot = snapshot;

    public IReadOnlyList<SelectedBanner> Select(string zoneAlias)
    {
        string alias = AliasNormalizer.Normalize(zoneAlias);
        if (!_snapshot.Current.Zones.TryGetValue(alias, out SnapshotZone? zone) || zone.Candidates.Count == 0)
        {
            return [];
        }

        // Uniform random choice of different candidates (weights and schedules come with US2).
        int count = Math.Min(zone.ShowCount, zone.Candidates.Count);
        SnapshotBanner[] pool = [.. zone.Candidates];
        Random.Shared.Shuffle(pool);
        var chosen = new SelectedBanner[count];
        for (int i = 0; i < count; i++)
        {
            SnapshotBanner banner = pool[i];
            chosen[i] = new SelectedBanner
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

        return chosen;
    }
}
