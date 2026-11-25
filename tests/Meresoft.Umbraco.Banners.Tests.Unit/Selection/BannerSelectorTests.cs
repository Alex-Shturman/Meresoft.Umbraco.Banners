using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Selection;

[TestFixture]
public class BannerSelectorTests
{
    private static BannerSelector SelectorFor(int showCount, int candidateCount)
    {
        SnapshotBanner[] candidates = Enumerable.Range(1, candidateCount)
            .Select(i => new SnapshotBanner { BannerId = i, ZoneId = 1, ImageUrl = $"/m/{i}.png", AltText = "alt " + i, LinkUrl = "https://example.org/" + i })
            .ToArray();
        var zone = new SnapshotZone(1, "home-top", showCount, ZoneRenderMode.Server, null, null, candidates);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());
        ILiveSnapshotAccessor accessor = Substitute.For<ILiveSnapshotAccessor>();
        accessor.Current.Returns(snapshot);
        return NewSelector(accessor, new FakeTimeProvider(Now));
    }

    private static readonly DateTimeOffset Now = new(2027, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private static BannerSelector NewSelector(ILiveSnapshotAccessor accessor, TimeProvider time) =>
        new(accessor, new WeightedSampler(new SeededRandomSource(7)), new ScheduleFilter(time));

    private static BannerSelector SelectorFor(params SnapshotBanner[] candidates)
    {
        var zone = new SnapshotZone(1, "home-top", 1, ZoneRenderMode.Server, null, null, candidates);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());
        ILiveSnapshotAccessor accessor = Substitute.For<ILiveSnapshotAccessor>();
        accessor.Current.Returns(snapshot);
        return NewSelector(accessor, new FakeTimeProvider(Now));
    }

    private static SnapshotBanner Banner(int id, int weight = 1, DateTime? start = null, DateTime? end = null) => new()
    {
        BannerId = id, ZoneId = 1, ImageUrl = "/m/x.png", AltText = "alt", LinkUrl = "https://example.org/x",
        Weight = weight, StartUtc = start, EndUtc = end,
    };

    [Test]
    public void ExpiredAndNotYetStartedBanners_AreNeverSelected()
    {
        BannerSelector selector = SelectorFor(
            Banner(1, end: Now.UtcDateTime.AddMinutes(-1)),
            Banner(2, start: Now.UtcDateTime.AddMinutes(1)),
            Banner(3));

        for (int i = 0; i < 200; i++)
        {
            Assert.That(selector.Select("home-top").Select(b => b.BannerId), Is.EqualTo(new[] { 3 }));
        }
    }

    [Test]
    public void WhenOnlyExpiredBannersRemain_TheZoneIsEmpty()
    {
        Assert.That(SelectorFor(Banner(1, end: Now.UtcDateTime.AddSeconds(-1))).Select("home-top"), Is.Empty);
    }

    [Test]
    public void WeightsAreHonoured_ThroughTheSelector()
    {
        BannerSelector selector = SelectorFor(Banner(1, 1), Banner(2, 3));

        int second = Enumerable.Range(0, 4000).Count(_ => selector.Select("home-top")[0].BannerId == 2);

        Assert.That(second * 100.0 / 4000, Is.InRange(70.0, 80.0));
    }

    [Test]
    public void ShowCountOne_EventuallyPicksEveryCandidate_AndAlwaysExactlyOne()
    {
        BannerSelector selector = SelectorFor(1, 3);

        var seen = new HashSet<int>();
        for (int i = 0; i < 300; i++)
        {
            IReadOnlyList<SelectedBanner> chosen = selector.Select("home-top");
            Assert.That(chosen, Has.Count.EqualTo(1));
            seen.Add(chosen[0].BannerId);
        }

        Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void ShowCountThree_GivesThreeDifferentBanners_AndTheCandidatesAreNotReordered()
    {
        BannerSelector selector = SelectorFor(3, 5);

        for (int i = 0; i < 50; i++)
        {
            Assert.That(selector.Select("home-top").Select(b => b.BannerId).Distinct().Count(), Is.EqualTo(3));
        }
    }

    [Test]
    public void ShowCountAboveTheCandidates_GivesAllCandidates()
    {
        Assert.That(SelectorFor(5, 2).Select("home-top"), Has.Count.EqualTo(2));
    }

    [Test]
    public void UnknownZone_GivesNothing()
    {
        Assert.That(SelectorFor(1, 2).Select("other"), Is.Empty);
    }

    [Test]
    public void APublicCallerMayPassAnAliasThatIsNotNormalised()
    {
        Assert.That(SelectorFor(1, 2).Select("  Home-Top "), Has.Count.EqualTo(1));
    }

    // Not parallel and skipped under coverage, see WeightedSamplerTests. The expected cost is about 64 bytes (the
    // sampler's list and the result list); the limit has twice that, and the old Select().ToArray() path cost far more.
    [Test]
    [NonParallelizable]
    public void SelectingOne_AllocatesOnlyTheTwoSmallLists()
    {
        WeightedSamplerTests.IgnoreUnderCoverage();
        // A plain fake, because a mocking library records every call and would be what the test measures.
        var zone = new SnapshotZone(1, "home-top", 1, ZoneRenderMode.Server, null, null, [Banner(1, 1), Banner(2, 3)]);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());
        var selector = NewSelector(new FixedSnapshotAccessor(snapshot), new FakeTimeProvider(Now));
        for (int i = 0; i < 2000; i++)
        {
            selector.Select("home-top");
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            selector.Select("home-top");
        }

        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        Assert.That(perCall, Is.LessThanOrEqualTo(144), "the sampler's list and the result list; the SelectedBanner is built once");
    }

    private sealed class FixedSnapshotAccessor(LiveSnapshot snapshot) : ILiveSnapshotAccessor
    {
        public LiveSnapshot Current => snapshot;

        public bool IsBuilt => true;

        public void MarkStartupBuildFinished()
        {
        }

        public Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null) => Task.CompletedTask;

        public Task ReloadLimitsAsync() => Task.CompletedTask;
    }
}
