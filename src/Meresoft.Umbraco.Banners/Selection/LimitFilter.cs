using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>
/// Takes banners out of the live snapshot once their impression or click limit is used up (US5). The totals are the
/// ones the database summed over all zones and days, imported rows included, so a banner in two zones leaves both
/// together. Applied when the snapshot is built and after each flush, never per page request.
/// </summary>
internal static class LimitFilter
{
    /// <summary>The candidates that are still under their limits. The same list when none is over a limit.</summary>
    public static IReadOnlyList<SnapshotBanner> Apply(IReadOnlyList<SnapshotBanner> candidates, IReadOnlyDictionary<int, BannerTotals> totals)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(totals);

        if (!candidates.Any(c => IsReached(c, totals)))
        {
            return candidates;
        }

        return [.. candidates.Where(c => !IsReached(c, totals))];
    }

    /// <summary>The snapshot without the banners over a limit. The same snapshot when no banner is over one.</summary>
    public static LiveSnapshot Apply(LiveSnapshot snapshot, IReadOnlyDictionary<int, BannerTotals> totals)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(totals);

        var zones = new Dictionary<string, SnapshotZone>(snapshot.Zones.Count, StringComparer.Ordinal);
        bool changed = false;
        foreach ((string alias, SnapshotZone zone) in snapshot.Zones)
        {
            IReadOnlyList<SnapshotBanner> kept = Apply(zone.Candidates, totals);
            changed |= !ReferenceEquals(kept, zone.Candidates);
            zones[alias] = ReferenceEquals(kept, zone.Candidates) ? zone : zone with { Candidates = kept };
        }

        return changed ? new LiveSnapshot(zones, snapshot.ImageMissingBannerIds, snapshot.MediaKeys) : snapshot;
    }

    /// <summary>The ids of the banners in the snapshot that have a limit, each once: the ones whose totals are worth reading.</summary>
    public static IReadOnlyCollection<int> LimitedBannerIds(LiveSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return [.. snapshot.Zones.Values
            .SelectMany(z => z.Candidates)
            .Where(c => c.ImpressionLimit is not null || c.ClickLimit is not null)
            .Select(c => c.BannerId)
            .Distinct()];
    }

    private static bool IsReached(SnapshotBanner banner, IReadOnlyDictionary<int, BannerTotals> totals)
    {
        if (banner.ImpressionLimit is null && banner.ClickLimit is null)
        {
            return false;
        }

        // A banner with no statistics rows has zero views, so it has no entry.
        totals.TryGetValue(banner.BannerId, out BannerTotals total);
        return BannerLimits.IsReached(banner.ImpressionLimit, banner.ClickLimit, total.Impressions, total.Clicks);
    }
}
