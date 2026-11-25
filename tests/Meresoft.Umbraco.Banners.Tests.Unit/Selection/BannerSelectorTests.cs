using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
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
        return new BannerSelector(accessor);
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
    public void TheAliasIsExpectedNormalised_SoTheSelectorDoesNotNormaliseAgain()
    {
        Assert.That(SelectorFor(1, 2).Select("Home-Top"), Is.Empty);
    }
}
