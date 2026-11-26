using Meresoft.Umbraco.Banners.Counting;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Counting;

[TestFixture]
public class CounterBufferTests
{
    private static readonly DateOnly Today = new(2027, 3, 1);

    private CounterBuffer _buffer = null!;

    [SetUp]
    public void SetUp() => _buffer = new CounterBuffer(new FakeTimeProvider(new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero)));

    [Test]
    public void ParallelIncrements_GiveExactTotalsPerDayBannerAndZone()
    {
        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            for (int i = 0; i < 1_000; i++)
            {
                _buffer.AddImpression(Today, 1, 10);
                _buffer.AddImpression(Today, 2, 10);
                if (i % 10 == 0)
                {
                    _buffer.AddClick(Today, 1, 10);
                }

                _buffer.AddImpression(Today.AddDays(1), 1, 11);
            }
        });

        IReadOnlyList<CounterEntry> entries = _buffer.Swap();

        Assert.That(entries, Has.Count.EqualTo(3));
        Assert.That(entries.Single(e => e.BannerId == 1 && e.ZoneId == 10), Is.EqualTo(new CounterEntry(Today, 1, 10, 64_000, 6_400)));
        Assert.That(entries.Single(e => e.BannerId == 2).Impressions, Is.EqualTo(64_000));
        Assert.That(entries.Single(e => e.ZoneId == 11), Is.EqualTo(new CounterEntry(Today.AddDays(1), 1, 11, 64_000, 0)));
    }

    [Test]
    public void Swap_ReturnsAllCountsAndLeavesAnEmptyBuffer()
    {
        _buffer.AddImpression(Today, 1, 10);
        _buffer.AddClick(Today, 1, 10);

        IReadOnlyList<CounterEntry> first = _buffer.Swap();
        IReadOnlyList<CounterEntry> second = _buffer.Swap();

        Assert.That(first, Is.EqualTo(new[] { new CounterEntry(Today, 1, 10, 1, 1) }));
        Assert.That(second, Is.Empty);
    }

    [Test]
    public void CountsAddedWhileSwapping_AreNeverLost()
    {
        const int perThread = 50_000;
        long swapped = 0;
        using var stop = new CancellationTokenSource();
        Task swapper = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                swapped += _buffer.Swap().Sum(e => e.Impressions);
            }
        });

        Parallel.For(0, 8, _ =>
        {
            for (int i = 0; i < perThread; i++)
            {
                _buffer.AddImpression(Today, 1, 10);
            }
        });
        stop.Cancel();
        swapper.Wait();
        swapped += _buffer.Swap().Sum(e => e.Impressions);

        Assert.That(swapped, Is.EqualTo(8L * perThread));
    }

    [Test]
    public void RestoringAFailedBatch_MergesItBackWithoutLoss()
    {
        _buffer.AddImpression(Today, 1, 10);
        _buffer.AddImpression(Today, 1, 10);
        IReadOnlyList<CounterEntry> failed = _buffer.Swap();
        _buffer.AddImpression(Today, 1, 10);
        _buffer.AddClick(Today, 2, 10);

        _buffer.Restore(failed);
        IReadOnlyList<CounterEntry> all = _buffer.Swap();

        Assert.That(all.Single(e => e.BannerId == 1).Impressions, Is.EqualTo(3));
        Assert.That(all.Single(e => e.BannerId == 2).Clicks, Is.EqualTo(1));
    }

    [Test]
    public void AnEmptyBuffer_SwapsToAnEmptyList()
    {
        Assert.That(_buffer.Swap(), Is.Empty);
    }
}
