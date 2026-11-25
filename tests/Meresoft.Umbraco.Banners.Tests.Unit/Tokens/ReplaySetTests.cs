using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Tokens;
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

    private ReplaySet Create(int maxEntries = 1_000) =>
        new(_clock, Options.Create(new BannersOptions { ViewTokenLifetime = Lifetime }), maxEntries);

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
}
