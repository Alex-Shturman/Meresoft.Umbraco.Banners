using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>
/// Remembers, in memory, the zone aliases that templates asked for but that do not exist (FR-003).
/// The flush job writes them to the database. Only valid aliases are kept and the set is capped, so
/// a template that builds aliases from user input cannot grow it without limit.
/// </summary>
public sealed class RequestedZoneRecorder
{
    private const int MaxEntries = 500;

    private readonly TimeProvider _timeProvider;
    private ConcurrentDictionary<string, (DateTime First, DateTime Last)> _seen = new(StringComparer.Ordinal);

    /// <summary>Creates the recorder.</summary>
    public RequestedZoneRecorder(TimeProvider timeProvider) => _timeProvider = timeProvider;

    /// <summary>Notes that a template asked for the alias now. Invalid aliases are ignored.</summary>
    public void Record(string alias)
    {
        string normalised = AliasNormalizer.Normalize(alias);
        if (!ZoneValidator.IsValidAlias(normalised))
        {
            return;
        }

        ConcurrentDictionary<string, (DateTime First, DateTime Last)> seen = _seen;
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        if (seen.Count >= MaxEntries && !seen.ContainsKey(normalised))
        {
            return;
        }

        seen.AddOrUpdate(normalised, _ => (now, now), (_, old) => (old.First, now));
    }

    /// <summary>The aliases noted so far, without clearing them.</summary>
    public IReadOnlyList<RequestedZone> Snapshot() =>
        _seen.Select(e => new RequestedZone(e.Key, e.Value.First, e.Value.Last)).ToList();

    /// <summary>Takes the aliases noted so far and starts again with an empty set.</summary>
    internal IReadOnlyList<RequestedZone> Drain()
    {
        ConcurrentDictionary<string, (DateTime First, DateTime Last)> taken =
            Interlocked.Exchange(ref _seen, new ConcurrentDictionary<string, (DateTime First, DateTime Last)>(StringComparer.Ordinal));
        return taken.Select(e => new RequestedZone(e.Key, e.Value.First, e.Value.Last)).ToList();
    }
}
