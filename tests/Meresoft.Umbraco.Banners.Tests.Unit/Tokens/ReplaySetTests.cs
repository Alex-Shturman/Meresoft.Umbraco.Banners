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

    [Test]
    public void TheFirstPairIsAccepted_TheSecondIsRefused()
    {
        ReplaySet set = Create();

        Assert.That(set.TryAdd(Nonce(1), 12), Is.True);
        Assert.That(set.TryAdd(Nonce(1), 12), Is.False);
    }

    [Test]
    public void ADifferentBannerWithTheSameNonce_IsAccepted()
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
    public void TheSetIsBounded_ExpiredEntriesAreMadeRoomFor_ButAFullSetOfLiveEntriesRefuses()
    {
        ReplaySet set = Create(maxEntries: 3);
        Assert.That(set.TryAdd(Nonce(1), 1), Is.True);
        Assert.That(set.TryAdd(Nonce(2), 1), Is.True);
        Assert.That(set.TryAdd(Nonce(3), 1), Is.True);

        Assert.That(set.TryAdd(Nonce(4), 1), Is.False, "full of live entries");

        _clock.Advance(Lifetime + TimeSpan.FromSeconds(1));
        Assert.That(set.TryAdd(Nonce(4), 1), Is.True, "the old entries were purged");
        Assert.That(set.Count, Is.LessThanOrEqualTo(3));
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

    // Review 5: a full set refuses new impressions, so it says so once, with counts only.

    [Test]
    public void AFullSet_LogsOneWarningWithCountsOnly_NotOnePerRefusedReport()
    {
        var logger = new CapturingLogger();
        ReplaySet set = Create(maxEntries: 2, logger);
        set.TryAdd(Nonce(1), 7);
        set.TryAdd(Nonce(2), 7);

        for (byte i = 3; i < 20; i++)
        {
            Assert.That(set.TryAdd(Nonce(i), 7), Is.False);
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
        Assert.That(set.TryAdd(Nonce(4), 7), Is.False, "full again");

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
}
