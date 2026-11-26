using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Tokens;

[TestFixture]
public class ReplaySetTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private FakeTimeProvider _clock = null!;

    [SetUp]
    public void SetUp() => _clock = new FakeTimeProvider(new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero));

    private ReplaySet Create(int maxEntries = 1_000, ILogger<ReplaySet>? logger = null) =>
        new(_clock, Options.Create(new BannersOptions { ViewTokenLifetime = Lifetime }), logger ?? NullLogger<ReplaySet>.Instance, maxEntries);

    private static byte[] Nonce(byte seed) => Enumerable.Repeat(seed, 12).ToArray();

    private static byte[] Token(int number)
    {
        byte[] nonce = new byte[12];
        BitConverter.TryWriteBytes(nonce, number);
        return nonce;
    }

    [Test]
    public void TheFirstPairIsAccepted_TheSecondIsRefused()
    {
        ReplaySet set = Create();

        Assert.That(set.TryAdd(Nonce(1), 12), Is.True);
        Assert.That(set.TryAdd(Nonce(1), 12), Is.False);
    }

    [Test]
    public void ADifferentPositionWithTheSameNonce_IsAccepted()
    {
        ReplaySet set = Create();
        set.TryAdd(Nonce(1), 12);

        Assert.That(set.TryAdd(Nonce(1), 15), Is.True);
    }

    [Test]
    public void ADifferentNonceWithTheSameBanner_IsAccepted()
    {
        ReplaySet set = Create();
        set.TryAdd(Nonce(1), 12);

        Assert.That(set.TryAdd(Nonce(2), 12), Is.True);
    }

    [Test]
    public void AnEntryExpiresWithTheTokenLifetime()
    {
        ReplaySet set = Create();
        set.TryAdd(Nonce(1), 12);

        _clock.Advance(Lifetime - TimeSpan.FromSeconds(1));
        Assert.That(set.TryAdd(Nonce(1), 12), Is.False, "still inside the lifetime");

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.That(set.TryAdd(Nonce(1), 12), Is.True, "the token itself has expired by now");
    }

    [Test]
    public void ATokenWithManyBanners_HoldsOneEntry_AndEachPositionCountsOnce()
    {
        ReplaySet set = Create();

        for (int position = 0; position < 10; position++)
        {
            Assert.That(set.TryAdd(Nonce(1), position), Is.True, $"position {position}");
        }

        Assert.That(set.Count, Is.EqualTo(1), "memory per token does not grow with its banners");
        for (int position = 0; position < 10; position++)
        {
            Assert.That(set.TryAdd(Nonce(1), position), Is.False, $"replay of position {position}");
        }
    }

    [Test]
    public void APositionBeyondTheMask_IsRefused()
    {
        ReplaySet set = Create();

        Assert.That(set.TryAdd(Nonce(1), 32), Is.False);
        Assert.That(set.TryAdd(Nonce(1), -1), Is.False);
        Assert.That(set.Count, Is.EqualTo(0));
    }

    [Test]
    public void AFullSet_StillAcceptsNewTokens_ByEvictingTheOnesClosestToExpiry()
    {
        ReplaySet set = Create(maxEntries: 100);

        // A flood of tokens, one second apart so their expiry differs, fills the set.
        for (int i = 0; i < 100; i++)
        {
            Assert.That(set.TryAdd(Token(i), 0), Is.True);
            _clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        // Real tokens arriving at a full set are accepted, and the set stays bounded.
        for (int i = 1000; i < 1050; i++)
        {
            Assert.That(set.TryAdd(Token(i), 0), Is.True, $"new token {i} must be accepted");
        }

        Assert.That(set.Count, Is.LessThanOrEqualTo(100));
        Assert.That(set.TryAdd(Token(1049), 0), Is.False, "the newest token is still remembered, a replay is refused");
        Assert.That(set.TryAdd(Token(0), 0), Is.True, "the oldest entry was evicted first");
    }

    [Test]
    public void AfterAFloodFillsTheSet_AReplayOfATokenStillPresentIsRefused()
    {
        ReplaySet set = Create(maxEntries: 100);
        for (int i = 0; i < 1_000; i++)
        {
            set.TryAdd(Token(i), 0);
            _clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        // The newest tokens expire last, so they are present and a replay of them is refused.
        for (int i = 990; i < 1_000; i++)
        {
            Assert.That(set.TryAdd(Token(i), 0), Is.False, $"replay of present token {i}");
        }
    }

    [Test]
    public void AFlood_KeepsTheSetBounded()
    {
        ReplaySet set = Create(maxEntries: 500);

        for (int i = 0; i < 20_000; i++)
        {
            set.TryAdd(Token(i), 0);
        }

        Assert.That(set.Count, Is.LessThanOrEqualTo(500));
    }

    [Test]
    public void ExpiredEntriesAreMadeRoomFor_BeforeAnyLiveEntryIsEvicted()
    {
        ReplaySet set = Create(maxEntries: 3);
        set.TryAdd(Nonce(1), 0);
        set.TryAdd(Nonce(2), 0);
        _clock.Advance(Lifetime - TimeSpan.FromSeconds(30));
        set.TryAdd(Nonce(3), 0);

        _clock.Advance(TimeSpan.FromSeconds(45));
        Assert.That(set.TryAdd(Nonce(4), 0), Is.True);

        Assert.That(set.TryAdd(Nonce(3), 0), Is.False, "the live token was kept");
    }

    [Test]
    public void ManyThreadsAddingTheSamePair_ExactlyOneWins()
    {
        ReplaySet set = Create();
        int accepted = 0;

        Parallel.For(0, 200, _ =>
        {
            if (set.TryAdd(Nonce(9), 5))
            {
                Interlocked.Increment(ref accepted);
            }
        });

        Assert.That(accepted, Is.EqualTo(1));
    }

    // A full set evicts the entries closest to expiry, so it says so once, with counts only.

    [Test]
    public void AFullSet_LogsOneWarningWithCountsOnly_NotOnePerEviction()
    {
        var logger = new CapturingLogger();
        ReplaySet set = Create(maxEntries: 2, logger);
        set.TryAdd(Nonce(1), 7);
        set.TryAdd(Nonce(2), 7);

        for (byte i = 3; i < 20; i++)
        {
            Assert.That(set.TryAdd(Nonce(i), 7), Is.True);
        }

        Assert.That(logger.Entries, Has.Count.EqualTo(1));
        Assert.That(logger.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(logger.Entries[0].Message, Does.Contain("2"), "the capacity");
        Assert.That(logger.Entries[0].Message, Does.Not.Contain("03030303"), "no nonce");
    }

    [Test]
    public void AfterTheSetDrained_AnotherFillWarnsAgain()
    {
        var logger = new CapturingLogger();
        ReplaySet set = Create(maxEntries: 1, logger);
        set.TryAdd(Nonce(1), 7);
        set.TryAdd(Nonce(2), 7);

        _clock.Advance(Lifetime + TimeSpan.FromMinutes(1));
        Assert.That(set.TryAdd(Nonce(3), 7), Is.True, "room again");
        Assert.That(set.TryAdd(Nonce(4), 7), Is.True, "full again, the oldest is evicted");

        Assert.That(logger.Entries, Has.Count.EqualTo(2));
    }

    [Test]
    public void ASetThatNeverFills_LogsNothing()
    {
        var logger = new CapturingLogger();
        ReplaySet set = Create(maxEntries: 10, logger);

        set.TryAdd(Nonce(1), 7);
        set.TryAdd(Nonce(1), 7);

        Assert.That(logger.Entries, Is.Empty);
    }

    private sealed class CapturingLogger : ILogger<ReplaySet>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>
    /// The cost of making room in a full set: it runs on the request that fills the set, so it must stay small. Filled with
    /// distinct expiry times (none expired), so the eviction has to sort and cut, not just drop expired entries.
    /// </summary>
    [Test]
    [Category("Perf")]
    public void EvictingFromAFullSetOf100000Tokens_IsQuick_AndLeavesAtMostNinetyPercent()
    {
        const int capacity = 100_000;
        ReplaySet set = Create(capacity);
        for (int i = 0; i < capacity; i++)
        {
            Assert.That(set.TryAdd(Token(i), 0), Is.True);
            _clock.Advance(TimeSpan.FromMilliseconds(1));
        }

        Assert.That(set.Count, Is.EqualTo(capacity));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool added = set.TryAdd(Token(capacity), 0);
        watch.Stop();

        Assert.That(added, Is.True, "the new token is never refused");
        Assert.That(set.Count, Is.LessThanOrEqualTo(capacity * 9 / 10 + 1), "cut down to 90% of capacity, plus the new token");
        Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500), "eviction of 100,000 entries");
    }
}
