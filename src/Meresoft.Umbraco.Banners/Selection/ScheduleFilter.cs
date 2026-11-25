using Meresoft.Umbraco.Banners.Caching;

namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>
/// Keeps the candidates whose window [StartUtc, EndUtc) contains now. Evaluated per request, because a start
/// or end time changes what shows without any publish (research R8).
/// </summary>
internal sealed class ScheduleFilter
{
    private readonly TimeProvider _timeProvider;

    public ScheduleFilter(TimeProvider timeProvider) => _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>True when the banner may show now. A missing start or end is an open end.</summary>
    public bool IsActive(SnapshotBanner banner) => IsActive(banner, _timeProvider.GetUtcNow().UtcDateTime);

    /// <summary>
    /// The active candidates in their order. When all are active the same list is returned, so the common
    /// case allocates nothing.
    /// </summary>
    public IReadOnlyList<SnapshotBanner> Active(IReadOnlyList<SnapshotBanner> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (IsActive(candidates[i], now))
            {
                continue;
            }

            // The first inactive candidate found: copy what was active before it, then filter the rest.
            var active = new List<SnapshotBanner>(candidates.Count - 1);
            for (int j = 0; j < i; j++)
            {
                active.Add(candidates[j]);
            }

            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (IsActive(candidates[j], now))
                {
                    active.Add(candidates[j]);
                }
            }

            return active;
        }

        return candidates;
    }

    private static bool IsActive(SnapshotBanner banner, DateTime nowUtc) =>
        (banner.StartUtc is not { } start || nowUtc >= start) && (banner.EndUtc is not { } end || nowUtc < end);
}
