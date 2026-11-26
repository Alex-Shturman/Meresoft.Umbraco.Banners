using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

[TestFixture]
public class SnapshotBannerTests
{
    private static SnapshotBanner Banner(string alt = "alt") => new()
    {
        BannerId = 1,
        ZoneId = 2,
        ImageUrl = "/m/1.png",
        AltText = alt,
        LinkUrl = "https://example.org/1",
        Weight = 3,
    };

    [Test]
    public void TwoEqualBanners_AreEqual_WhetherOrNotSelectedWasReadOnOne()
    {
        SnapshotBanner read = Banner();
        SnapshotBanner untouched = Banner();

        _ = read.Selected;

        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(untouched));
            Assert.That(read.GetHashCode(), Is.EqualTo(untouched.GetHashCode()));
        });
    }

    [Test]
    public void Selected_IsBuiltOnce_AndCarriesTheFieldsOfTheBanner()
    {
        SnapshotBanner banner = Banner("hello");

        Assert.Multiple(() =>
        {
            Assert.That(banner.Selected, Is.SameAs(banner.Selected));
            Assert.That(banner.Selected.BannerId, Is.EqualTo(1));
            Assert.That(banner.Selected.ZoneId, Is.EqualTo(2));
            Assert.That(banner.Selected.AltText, Is.EqualTo("hello"));
            Assert.That(banner.Selected.LinkUrl, Is.EqualTo("https://example.org/1"));
        });
    }

    [Test]
    public void AWithCopy_GetsItsOwnSelected_NotTheStaleOneOfTheOriginal()
    {
        SnapshotBanner original = Banner("before");
        _ = original.Selected;

        SnapshotBanner copy = original with { AltText = "after" };

        Assert.Multiple(() =>
        {
            Assert.That(copy.Selected.AltText, Is.EqualTo("after"));
            Assert.That(original.Selected.AltText, Is.EqualTo("before"));
            Assert.That(copy.Selected, Is.Not.SameAs(original.Selected));
            Assert.That(copy, Is.Not.EqualTo(original));
        });
    }

    [Test]
    public void ASnapshotBuildsEveryCandidatesSelectedUpFront()
    {
        SnapshotBanner banner = Banner();
        var zone = new SnapshotZone(2, "home-top", 1, ZoneRenderMode.Server, null, null, [banner]);

        _ = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());

        Assert.That(banner.IsSelectedBuilt, Is.True, "no page request has to build it");
    }
}
