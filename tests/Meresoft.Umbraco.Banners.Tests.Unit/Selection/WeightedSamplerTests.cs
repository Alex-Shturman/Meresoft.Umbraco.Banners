using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Selection;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Selection;

[TestFixture]
public class WeightedSamplerTests
{
    private static SnapshotBanner Candidate(int id, int weight = 1) => new()
    {
        BannerId = id,
        ZoneId = 1,
        ImageUrl = $"/m/{id}.png",
        AltText = "alt " + id,
        LinkUrl = "https://example.org/" + id,
        Weight = weight,
    };

    private static WeightedSampler Sampler(int seed = 20270301) => new(new SeededRandomSource(seed));

    [Test]
    public void WeightsOneAndThree_ShowCountOne_SharesAreWithinFivePointsOfAQuarterAndThreeQuarters()
    {
        WeightedSampler sampler = Sampler();
        SnapshotBanner[] candidates = [Candidate(1, 1), Candidate(2, 3)];

        const int draws = 4000;
        int first = 0;
        for (int i = 0; i < draws; i++)
        {
            if (sampler.Pick(candidates, 1)[0].BannerId == 1)
            {
                first++;
            }
        }

        double shareOfFirst = first * 100.0 / draws;
        Assert.That(shareOfFirst, Is.InRange(20.0, 30.0), "weight 1 of 4 is 25%");
        Assert.That(100.0 - shareOfFirst, Is.InRange(70.0, 80.0), "weight 3 of 4 is 75%");
    }

    [Test]
    public void ShowCountThreeOfFive_GivesThreeDifferentBannersEveryTime()
    {
        WeightedSampler sampler = Sampler();
        SnapshotBanner[] candidates = [Candidate(1), Candidate(2, 5), Candidate(3, 2), Candidate(4, 10), Candidate(5, 100)];

        for (int i = 0; i < 500; i++)
        {
            IReadOnlyList<SnapshotBanner> picked = sampler.Pick(candidates, 3);
            Assert.That(picked.Select(b => b.BannerId).Distinct().Count(), Is.EqualTo(3));
        }
    }

    [Test]
    public void ShowCountThreeWithOneCandidate_GivesThatOne()
    {
        IReadOnlyList<SnapshotBanner> picked = Sampler().Pick([Candidate(7)], 3);

        Assert.That(picked.Select(b => b.BannerId), Is.EqualTo(new[] { 7 }));
    }

    [Test]
    public void NoCandidates_GivesAnEmptyList()
    {
        Assert.That(Sampler().Pick([], 3), Is.Empty);
    }

    [Test]
    public void TheCallersListIsNotChanged()
    {
        SnapshotBanner[] candidates = [Candidate(1), Candidate(2), Candidate(3)];

        Sampler().Pick(candidates, 3);

        Assert.That(candidates.Select(b => b.BannerId), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void ShowCountEqualToTheCandidates_GivesEveryCandidateOnce()
    {
        SnapshotBanner[] candidates = [Candidate(1, 1), Candidate(2, 50), Candidate(3, 100)];

        IReadOnlyList<SnapshotBanner> picked = Sampler().Pick(candidates, 3);

        Assert.That(picked.Select(b => b.BannerId), Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void AWeightBelowOne_IsStillPicked_AndNeverThrows(int weight)
    {
        WeightedSampler sampler = Sampler();
        SnapshotBanner[] candidates = [Candidate(1, weight), Candidate(2, weight)];

        var seen = new HashSet<int>();
        for (int i = 0; i < 200; i++)
        {
            seen.Add(sampler.Pick(candidates, 1)[0].BannerId);
        }

        Assert.That(seen, Is.EquivalentTo(new[] { 1, 2 }), "a bad weight counts as 1, so both can show");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ACountBelowOne_GivesAnEmptyList(int count)
    {
        Assert.That(Sampler().Pick([Candidate(1), Candidate(2)], count), Is.Empty);
    }

    [Test]
    public void WeightsOneAndThree_ShowCountTwo_AlwaysGiveBoth()
    {
        WeightedSampler sampler = Sampler();
        SnapshotBanner[] candidates = [Candidate(1, 1), Candidate(2, 3)];

        for (int i = 0; i < 200; i++)
        {
            Assert.That(sampler.Pick(candidates, 2).Select(b => b.BannerId), Is.EquivalentTo(new[] { 1, 2 }));
        }
    }

    [Test]
    public void TwoBannersWithWeightsBelowOne_ShowCountTwo_GiveBoth()
    {
        SnapshotBanner[] candidates = [Candidate(1, 0), Candidate(2, -5)];

        Assert.That(Sampler().Pick(candidates, 2).Select(b => b.BannerId), Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void AWeightAboveTheEditorLimit_DoesNotOverflowTheSum()
    {
        SnapshotBanner[] candidates = [Candidate(1, int.MaxValue), Candidate(2, int.MaxValue), Candidate(3, int.MaxValue)];

        Assert.That(Sampler().Pick(candidates, 3).Select(b => b.BannerId), Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void PickingOneOfSeveral_AllocatesLittleMoreThanTheResult()
    {
        WeightedSampler sampler = Sampler();
        SnapshotBanner[] candidates = [Candidate(1, 1), Candidate(2, 3), Candidate(3, 5), Candidate(4, 2)];
        for (int i = 0; i < 2000; i++)
        {
            sampler.Pick(candidates, 1); // Warm up the JIT so tiering does not count.
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            sampler.Pick(candidates, 1);
        }

        long perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        Assert.That(perCall, Is.LessThanOrEqualTo(32), "only the one element result list, no copy of the candidates");
    }
}
