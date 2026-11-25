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
        IReadOnlyList<SnapshotBanner> picked = _sampler.Pick(active, zone.ShowCount);
        var result = new SelectedBanner[picked.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = picked[i].Selected;
        }

        return result;
    }
}
