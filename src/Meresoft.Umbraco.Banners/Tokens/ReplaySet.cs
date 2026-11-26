using System.Buffers.Binary;
using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Tokens;

/// <summary>
/// Remembers which banners of which view tokens were already counted, so an impression counts once. One entry per token
/// nonce holds the expiry and a bitmask of the banner positions already counted, so a token costs one entry however many
/// banners it carries. Bounded and thread safe: when full, the entries closest to expiry are evicted, so a flood of
/// tokens cannot keep real impressions out.
/// <para>
/// Evicting is a deliberate trade, not a fail-open hole: anyone can get fresh valid tokens for free from a page view or
/// from the zone delivery endpoint, so replaying an evicted token gives an attacker nothing they did not already have,
/// while refusing new tokens when the set is full would let the same attacker suppress every real impression. Only
/// the entries that expire soonest are evicted, never the newest. This set does not defend against token farming; the
/// bot filter and the viewability rule are the defences there.
/// </para>
/// </summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public sealed class ReplaySet
{
    private const int NonceLength = 12;

    /// <summary>The bitmask holds this many banner positions; a view token carries at most 10 banners.</summary>
    private const int MaxPositions = 32;

    /// <summary>A full set is cut down to this share of its capacity, so eviction runs once per many additions.</summary>
    private const double EvictionTargetShare = 0.9;

    private readonly ConcurrentDictionary<Key, Entry> _entries = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReplaySet> _logger;
    private readonly long _lifetimeTicks;
    private readonly long _purgeIntervalTicks;
    private readonly int _maxEntries;
    private long _nextPurgeTicks;
    private int _purging;
    private int _fullLogged;

    /// <summary>Creates the set. <paramref name="maxEntries"/> is the number of tokens held, not of pairs.</summary>
    public ReplaySet(TimeProvider timeProvider, IOptions<BannersOptions> options, ILogger<ReplaySet> logger, int maxEntries = 100_000)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        _timeProvider = timeProvider;
        _logger = logger;
        _lifetimeTicks = options.Value.ViewTokenLifetime.Ticks;
        _purgeIntervalTicks = Math.Max(1, _lifetimeTicks / 2);
        _maxEntries = maxEntries;
        _nextPurgeTicks = timeProvider.GetUtcNow().UtcTicks + _purgeIntervalTicks;
    }

    /// <summary>Number of tokens held, expired ones included until purged.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// True the first time a banner position of a token is seen (or after the entry expired); false for a repeat or for a
    /// position outside the mask. <paramref name="position"/> is the index of the banner in the token's banner list.
    /// </summary>
    public bool TryAdd(byte[] nonce, int position)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        if (nonce.Length != NonceLength)
        {
            throw new ArgumentException("The nonce must be 12 bytes.", nameof(nonce));
        }

        if (position is < 0 or >= MaxPositions)
        {
            return false;
        }

        long now = _timeProvider.GetUtcNow().UtcTicks;
        if (now >= Interlocked.Read(ref _nextPurgeTicks))
        {
            Purge(now);
        }

        var key = new Key(BinaryPrimitives.ReadInt64LittleEndian(nonce), BinaryPrimitives.ReadInt32LittleEndian(nonce.AsSpan(8)));
        uint bit = 1u << position;
        long expiry = now + _lifetimeTicks;

        while (true)
        {
            if (_entries.TryGetValue(key, out Entry existing))
            {
                bool expired = existing.Expiry <= now;

                // A live entry with the bit set is a replay; an expired entry starts over.
                if (!expired && (existing.Mask & bit) != 0)
                {
                    return false;
                }

                Entry updated = expired ? new Entry(expiry, bit) : new Entry(existing.Expiry, existing.Mask | bit);
                if (_entries.TryUpdate(key, updated, existing))
                {
                    return true;
                }

                continue;
            }

            if (_entries.Count >= _maxEntries)
            {
                EvictClosestToExpiry(now);
            }

            if (_entries.TryAdd(key, new Entry(expiry, bit)))
            {
                return true;
            }
        }
    }

    private void Purge(long now)
    {
        if (Interlocked.CompareExchange(ref _purging, 1, 0) != 0)
        {
            return;
        }

        try
        {
            RemoveExpired(now);
            Interlocked.Exchange(ref _nextPurgeTicks, now + _purgeIntervalTicks);
            if (_entries.Count < _maxEntries)
            {
                Volatile.Write(ref _fullLogged, 0);
            }
        }
        finally
        {
            Volatile.Write(ref _purging, 0);
        }
    }

    /// <summary>
    /// Makes room in a full set: expired entries go first, then, if the set is still full, the entries with the earliest
    /// expiry until it holds <see cref="EvictionTargetShare"/> of its capacity. Runs once per many additions, one thread at a time.
    /// </summary>
    private void EvictClosestToExpiry(long now)
    {
        if (Interlocked.CompareExchange(ref _purging, 1, 0) != 0)
        {
            return;
        }

        try
        {
            RemoveExpired(now);

            int excess = _entries.Count - (int)(_maxEntries * EvictionTargetShare);
            if (excess <= 0)
            {
                Volatile.Write(ref _fullLogged, 0);
                return;
            }

            // Said once per time the set fills up, with counts only: nothing of a token or a visitor goes into the log.
            if (Interlocked.Exchange(ref _fullLogged, 1) == 0)
            {
                _logger.LogWarning(
                    "The banner view token replay set is full ({Capacity} tokens) and now drops the tokens closest to expiry to make room. Tokens last {Lifetime}.",
                    _maxEntries,
                    TimeSpan.FromTicks(_lifetimeTicks));
            }

            long[] expiries = _entries.Values.Select(e => e.Expiry).ToArray();
            Array.Sort(expiries);
            long threshold = expiries[Math.Min(excess, expiries.Length) - 1];
            foreach (KeyValuePair<Key, Entry> entry in _entries)
            {
                if (entry.Value.Expiry <= threshold)
                {
                    _entries.TryRemove(entry);
                }
            }
        }
        finally
        {
            Volatile.Write(ref _purging, 0);
        }
    }

    private void RemoveExpired(long now)
    {
        foreach (KeyValuePair<Key, Entry> entry in _entries)
        {
            if (entry.Value.Expiry <= now)
            {
                _entries.TryRemove(entry);
            }
        }
    }

    private readonly record struct Key(long NonceLow, int NonceHigh);

    private readonly record struct Entry(long Expiry, uint Mask);
}
