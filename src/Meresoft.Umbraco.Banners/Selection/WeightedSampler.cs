using Meresoft.Umbraco.Banners.Caching;

namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>
/// Weighted random choice without replacement: draw by weight, remove the drawn candidate, repeat
/// (research R9). With at most tens of candidates this costs microseconds.
/// </summary>
internal sealed class WeightedSampler
{
    private readonly IRandomSource _random;

    public WeightedSampler(IRandomSource random) => _random = random ?? throw new ArgumentNullException(nameof(random));

    /// <summary>Up to <paramref name="count"/> different candidates. The caller's list is not changed.</summary>
    public IReadOnlyList<SnapshotBanner> Pick(IReadOnlyList<SnapshotBanner> candidates, int count)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        int take = Math.Min(count, candidates.Count);
        if (take <= 0)
        {
            return [];
        }

        if (candidates.Count == 1)
        {
            return [candidates[0]];
        }

        // Work on a copy so the snapshot's shared list stays untouched.
        var pool = new SnapshotBanner[candidates.Count];
        int total = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = candidates[i];
            total += WeightOf(pool[i]);
        }

        var picked = new SnapshotBanner[take];
        int remaining = pool.Length;
        for (int n = 0; n < take; n++)
        {
            int ticket = _random.Next(total);
            int index = 0;
            for (; index < remaining - 1; index++)
            {
                int weight = WeightOf(pool[index]);
                if (ticket < weight)
                {
                    break;
                }

                ticket -= weight;
            }

            SnapshotBanner chosen = pool[index];
            picked[n] = chosen;
            total -= WeightOf(chosen);
            pool[index] = pool[--remaining]; // Swap the last one in; order inside the pool does not matter.
        }

        return picked;
    }

    // The editor allows 1 to 100; anything lower is treated as 1 so a bad value never breaks the draw.
    private static int WeightOf(SnapshotBanner banner) => Math.Max(1, banner.Weight);
}
