using System.Buffers.Binary;
using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Tokens;

/// <summary>Remembers which (token nonce, banner) pairs were already counted, so an impression counts once. Bounded and thread safe.</summary>
public sealed class ReplaySet
{
    private const int NonceLength = 12;

    private readonly ConcurrentDictionary<Key, long> _entries = new();
    private readonly TimeProvider _timeProvider;
    private readonly long _lifetimeTicks;
    private readonly long _purgeIntervalTicks;
    private readonly int _maxEntries;
    private long _nextPurgeTicks;
    private int _purging;

    /// <summary>Creates the set.</summary>
    public ReplaySet(TimeProvider timeProvider, IOptions<BannersOptions> options, int maxEntries = 250_000)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider;
        _lifetimeTicks = options.Value.ViewTokenLifetime.Ticks;
        _purgeIntervalTicks = Math.Max(1, _lifetimeTicks / 2);
        _maxEntries = maxEntries;
        _nextPurgeTicks = timeProvider.GetUtcNow().UtcTicks + _purgeIntervalTicks;
    }

    /// <summary>Number of entries held, expired ones included until purged.</summary>
    public int Count => _entries.Count;

    /// <summary>True the first time a pair is seen (or after its entry expired); false for a repeat or when the set is full of live entries.</summary>
    public bool TryAdd(byte[] nonce, int bannerId)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        if (nonce.Length != NonceLength)
        {
            throw new ArgumentException("The nonce must be 12 bytes.", nameof(nonce));
        }

        long now = _timeProvider.GetUtcNow().UtcTicks;
        if (now >= Interlocked.Read(ref _nextPurgeTicks))
        {
            Purge(now);
        }

        var key = new Key(BinaryPrimitives.ReadInt64LittleEndian(nonce), BinaryPrimitives.ReadInt32LittleEndian(nonce.AsSpan(8)), bannerId);
        long expiry = now + _lifetimeTicks;

        if (_entries.TryGetValue(key, out long existing))
        {
            return existing <= now && _entries.TryUpdate(key, expiry, existing);
        }

        if (_entries.Count >= _maxEntries)
        {
            Purge(now);
            if (_entries.Count >= _maxEntries)
            {
                return false;
            }
        }

        return _entries.TryAdd(key, expiry);
    }

    private void Purge(long now)
    {
        if (Interlocked.CompareExchange(ref _purging, 1, 0) != 0)
        {
            return;
        }

        try
        {
            foreach (KeyValuePair<Key, long> entry in _entries)
            {
                if (entry.Value <= now)
                {
                    _entries.TryRemove(entry);
                }
            }

            Interlocked.Exchange(ref _nextPurgeTicks, now + _purgeIntervalTicks);
        }
        finally
        {
            Volatile.Write(ref _purging, 0);
        }
    }

    private readonly record struct Key(long NonceLow, int NonceHigh, int BannerId);
}
