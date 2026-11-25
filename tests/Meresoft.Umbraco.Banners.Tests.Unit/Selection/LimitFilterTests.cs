using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Selection;

[TestFixture]
public class LimitFilterTests
{
    private static SnapshotBanner Banner(int id, long? impressionLimit = null, long? clickLimit = null, int zoneId = 1) => new()
    {
        BannerId = id,
        ZoneId = zoneId,
        ImageUrl = "/m/x.png",
        AltText = "alt",
        LinkUrl = "https://example.org/x",
        ImpressionLimit = impressionLimit,
        ClickLimit = clickLimit,
    };

    private static IReadOnlyDictionary<int, BannerTotals> Totals(params (int Id, long Impressions, long Clicks)[] rows) =>
        rows.ToDictionary(r => r.Id, r => new BannerTotals(r.Impressions, r.Clicks));

    private static IReadOnlyList<int> Kept(IReadOnlyList<SnapshotBanner> candidates, IReadOnlyDictionary<int, BannerTotals> totals) =>
        [.. LimitFilter.Apply(candidates, totals).Select(b => b.BannerId)];

    private static LiveSnapshot Snapshot(params SnapshotZone[] zones) =>
        new(zones.ToDictionary(z => z.Alias), new HashSet<int>(), new HashSet<Guid>());

    [Test]
    public void ImpressionsBelowTheLimit_StayCandidates() =>
        Assert.That(Kept([Banner(1, impressionLimit: 3)], Totals((1, 2, 0))), Is.EqualTo(new[] { 1 }));

    [Test]
    public void ImpressionsAtTheLimit_AreNotCandidates() =>
        Assert.That(Kept([Banner(1, impressionLimit: 3)], Totals((1, 3, 0))), Is.Empty);

    [Test]
    public void ImpressionsAboveTheLimit_AreNotCandidates() =>
        Assert.That(Kept([Banner(1, impressionLimit: 3)], Totals((1, 9, 0))), Is.Empty);

    [Test]
    public void ClicksAtTheClickLimit_AreNotCandidates_EvenWithFewImpressions() =>
        Assert.That(Kept([Banner(1, impressionLimit: 100, clickLimit: 2)], Totals((1, 1, 2))), Is.Empty);

    [Test]
    public void ClicksBelowTheClickLimit_StayCandidates() =>
        Assert.That(Kept([Banner(1, clickLimit: 2)], Totals((1, 500, 1))), Is.EqualTo(new[] { 1 }));

    [Test]
    public void NullLimits_NeverBlock() =>
        Assert.That(Kept([Banner(1)], Totals((1, long.MaxValue, long.MaxValue))), Is.EqualTo(new[] { 1 }));

    [Test]
    public void ABannerWithoutAnyStatistics_HasZeroTotals_AndStaysACandidate() =>
        Assert.That(Kept([Banner(1, impressionLimit: 1)], Totals()), Is.EqualTo(new[] { 1 }));

    [Test]
    public void OnlyTheBannerOverItsLimitIsRemoved_TheOrderOfTheRestIsKept() =>
        Assert.That(
            Kept([Banner(1, impressionLimit: 5), Banner(2, impressionLimit: 5), Banner(3)], Totals((1, 1, 0), (2, 5, 0), (3, 99, 0))),
            Is.EqualTo(new[] { 1, 3 }));

    [Test]
    public void WhenNothingIsOverALimit_TheSameListIsReturned()
    {
        IReadOnlyList<SnapshotBanner> candidates = [Banner(1, impressionLimit: 5), Banner(2)];

        Assert.That(LimitFilter.Apply(candidates, Totals((1, 4, 0))), Is.SameAs(candidates));
    }

    [Test]
    public void TheTotalsAreOfTheBannerNotOfTheZone_SoTheSameBannerLeavesEveryZone()
    {
        LiveSnapshot snapshot = Snapshot(
            new SnapshotZone(1, "a", 1, ZoneRenderMode.Server, null, null, [Banner(7, impressionLimit: 3, zoneId: 1)]),
            new SnapshotZone(2, "b", 1, ZoneRenderMode.Server, null, null, [Banner(7, impressionLimit: 3, zoneId: 2), Banner(8, zoneId: 2)]));

        // The total of banner 7 is what the database summed over both zones (imported rows included).
        LiveSnapshot result = LimitFilter.Apply(snapshot, Totals((7, 3, 0)));

        Assert.That(result.Zones["a"].Candidates, Is.Empty);
        Assert.That(result.Zones["b"].Candidates.Select(c => c.BannerId), Is.EqualTo(new[] { 8 }));
        Assert.That(result.Zones["b"].ShowCount, Is.EqualTo(1));
    }

    [Test]
    public void ASnapshotWithNothingOverALimit_IsReturnedAsIs()
    {
        LiveSnapshot snapshot = Snapshot(new SnapshotZone(1, "a", 1, ZoneRenderMode.Server, null, null, [Banner(7, impressionLimit: 3)]));

        Assert.That(LimitFilter.Apply(snapshot, Totals((7, 2, 0))), Is.SameAs(snapshot));
    }

    [Test]
    public void LimitedBannerIds_ListsEachBannerWithALimitOnce()
    {
        LiveSnapshot snapshot = Snapshot(
            new SnapshotZone(1, "a", 1, ZoneRenderMode.Server, null, null, [Banner(7, impressionLimit: 3), Banner(8)]),
            new SnapshotZone(2, "b", 1, ZoneRenderMode.Server, null, null, [Banner(7, impressionLimit: 3, zoneId: 2), Banner(9, clickLimit: 4, zoneId: 2)]));

        Assert.That(LimitFilter.LimitedBannerIds(snapshot), Is.EquivalentTo(new[] { 7, 9 }));
    }
}
