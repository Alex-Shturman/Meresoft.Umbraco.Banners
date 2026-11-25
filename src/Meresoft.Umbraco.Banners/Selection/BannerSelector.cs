using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Selection;

/// <inheritdoc cref="IBannerSelector" />
internal sealed class BannerSelector : IBannerSelector
{
    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly WeightedSampler _sampler;
    private readonly ScheduleFilter _schedule;

    public BannerSelector(ILiveSnapshotAccessor snapshot, WeightedSampler sampler, ScheduleFilter schedule)
    {
        _snapshot = snapshot;
        _sampler = sampler;
        _schedule = schedule;
    }

    public IReadOnlyList<SelectedBanner> Select(string zoneAlias)
    {
        // Normalising an alias that is already normalised allocates nothing, so the renderer, which normalises
        // once itself, pays no real cost here, and a public caller may pass any spelling.
        if (!_snapshot.Current.Zones.TryGetValue(AliasNormalizer.Normalize(zoneAlias), out SnapshotZone? zone) || zone.Candidates.Count == 0)
        {
            return [];
        }

        // The schedule is judged now, per request: a start or end time changes what shows without a publish.
        IReadOnlyList<SnapshotBanner> active = _schedule.Active(zone.Candidates);
        return _sampler.Pick(active, zone.ShowCount).Select(ToSelected).ToArray();
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
