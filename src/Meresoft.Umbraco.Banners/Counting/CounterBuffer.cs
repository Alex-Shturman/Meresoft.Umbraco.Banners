using System.Collections.Concurrent;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>Counts of one banner in one zone on one day.</summary>
public readonly record struct CounterEntry(DateOnly Day, int BannerId, int ZoneId, long Impressions, long Clicks);

/// <summary>
/// Lock free in memory counters. The dictionary is never replaced and counters are only changed with
/// Interlocked operations, so a swap running next to writers never loses a count.
/// </summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public sealed class CounterBuffer
{
    private readonly ConcurrentDictionary<(DateOnly Day, int BannerId, int ZoneId), Counter> _counters = new();
    private readonly TimeProvider _timeProvider;

    public CounterBuffer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void AddImpression(DateOnly day, int bannerId, int zoneId) =>
        Interlocked.Increment(ref GetCounter(day, bannerId, zoneId).Impressions);

    public void AddClick(DateOnly day, int bannerId, int zoneId) =>
        Interlocked.Increment(ref GetCounter(day, bannerId, zoneId).Clicks);

    /// <summary>Takes all counts out of the buffer and resets them to zero.</summary>
    public IReadOnlyList<CounterEntry> Swap()
    {
        var result = new List<CounterEntry>();
        foreach (KeyValuePair<(DateOnly Day, int BannerId, int ZoneId), Counter> pair in _counters)
        {
            long impressions = Interlocked.Exchange(ref pair.Value.Impressions, 0);
            long clicks = Interlocked.Exchange(ref pair.Value.Clicks, 0);
            if (impressions != 0 || clicks != 0)
            {
                result.Add(new CounterEntry(pair.Key.Day, pair.Key.BannerId, pair.Key.ZoneId, impressions, clicks));
            }
        }

        DateOnly threshold = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime).AddDays(-2);
        foreach (KeyValuePair<(DateOnly Day, int BannerId, int ZoneId), Counter> pair in _counters)
        {
            if (pair.Key.Day < threshold
                && Volatile.Read(ref pair.Value.Impressions) == 0
                && Volatile.Read(ref pair.Value.Clicks) == 0)
            {
                _counters.TryRemove(pair.Key, out _);
            }
        }

        return result;
    }

    /// <summary>Adds the counts of a batch that could not be saved back into the buffer.</summary>
    public void Restore(IEnumerable<CounterEntry> entries)
    {
        foreach (CounterEntry entry in entries)
        {
            Counter counter = GetCounter(entry.Day, entry.BannerId, entry.ZoneId);
            Interlocked.Add(ref counter.Impressions, entry.Impressions);
            Interlocked.Add(ref counter.Clicks, entry.Clicks);
        }
    }

    private Counter GetCounter(DateOnly day, int bannerId, int zoneId)
    {
        var key = (day, bannerId, zoneId);
        return _counters.TryGetValue(key, out Counter? counter)
            ? counter
            : _counters.GetOrAdd(key, static _ => new Counter());
    }

    private sealed class Counter
    {
        public long Impressions;
        public long Clicks;
    }
}
