using Meresoft.Umbraco.Banners.Domain;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Domain;

[TestFixture]
public class BannerStateCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2027, 5, 10, 12, 0, 0, TimeSpan.Zero);
    private FakeTimeProvider _time = null!;
    private BannerStateCalculator _calculator = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _calculator = new BannerStateCalculator(_time);
    }

    private static BannerVersion Version(BannerVersionKind kind, DateTime savedUtc, params int[] zoneIds) => new()
    {
        Kind = kind,
        SavedDate = savedUtc,
        ZoneIds = [.. zoneIds],
        MediaKey = Guid.NewGuid(),
    };

    private static Banner Banner(BannerVersion? draft, BannerVersion? live, bool isPublished, DateTime? publishDate) => new()
    {
        Type = BannerType.Image,
        Draft = draft,
        Live = live,
        IsPublished = isPublished,
        PublishDate = publishDate,
    };

    // ---- publish state (data-model.md "Banner publish state") ----

    [Test]
    public void State_Draft_WhenNoLiveVersion()
    {
        Banner banner = Banner(Version(BannerVersionKind.Draft, Now.UtcDateTime, 1), null, false, null);

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.Draft));
    }

    [Test]
    public void State_Published_WhenDraftAndLiveWereSavedAtTheSameTime()
    {
        DateTime saved = Now.UtcDateTime;
        Banner banner = Banner(
            Version(BannerVersionKind.Draft, saved, 1),
            Version(BannerVersionKind.Live, saved, 1),
            true,
            saved.AddMinutes(1));

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.Published));
    }

    [Test]
    public void State_PublishedWithChanges_WhenTheSavedDatesDiffer_WhateverThePublishDateSays()
    {
        DateTime saved = Now.UtcDateTime;
        Banner banner = Banner(
            Version(BannerVersionKind.Draft, saved.AddMilliseconds(10), 1),
            Version(BannerVersionKind.Live, saved, 1),
            true,
            saved.AddHours(1));

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.PublishedWithChanges));
    }

    [Test]
    public void State_IgnoresThePublishDate_SoClockSkewBetweenServersDoesNotMatter()
    {
        // Publish ran on a server whose clock is two minutes behind the server that saved the draft:
        // the publish date is earlier than the draft's saved date, yet the draft was published unchanged.
        DateTime saved = Now.UtcDateTime;
        Banner skewed = Banner(
            Version(BannerVersionKind.Draft, saved, 1),
            Version(BannerVersionKind.Live, saved, 1),
            true,
            saved.AddMinutes(-2));

        Assert.That(_calculator.GetState(skewed), Is.EqualTo(BannerState.Published));
    }

    [Test]
    public void State_PublishedWithChanges_WhenDraftSavedAfterPublish()
    {
        DateTime published = Now.UtcDateTime;
        Banner banner = Banner(
            Version(BannerVersionKind.Draft, published.AddSeconds(1), 1),
            Version(BannerVersionKind.Live, published, 1),
            true,
            published);

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.PublishedWithChanges));
    }

    [Test]
    public void State_Unpublished_WhenLiveExistsButNotPublished()
    {
        DateTime published = Now.UtcDateTime.AddDays(-1);
        Banner banner = Banner(
            Version(BannerVersionKind.Draft, published, 1),
            Version(BannerVersionKind.Live, published, 1),
            false,
            published);

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.Unpublished));
    }

    [Test]
    public void State_Unpublished_EvenWhenDraftHasNewerChanges()
    {
        DateTime published = Now.UtcDateTime.AddDays(-1);
        Banner banner = Banner(
            Version(BannerVersionKind.Draft, Now.UtcDateTime, 1),
            Version(BannerVersionKind.Live, published, 1),
            false,
            published);

        Assert.That(_calculator.GetState(banner), Is.EqualTo(BannerState.Unpublished));
    }

    // ---- runtime flags (data-model.md "Banner runtime flags") ----

    private static BannerRuntimeInfo Info(bool mediaExists = true, long impressions = 0, long clicks = 0) =>
        new(mediaExists, impressions, clicks);

    private Banner PublishedBanner(Action<BannerVersion>? change = null, params int[] zoneIds)
    {
        DateTime published = Now.UtcDateTime.AddHours(-1);
        BannerVersion live = Version(BannerVersionKind.Live, published, zoneIds.Length == 0 ? [1] : zoneIds);
        change?.Invoke(live);
        return Banner(Version(BannerVersionKind.Draft, published, 1), live, true, published);
    }

    [Test]
    public void Flags_None_ForHealthyPublishedBanner()
    {
        Assert.That(_calculator.GetFlags(PublishedBanner(), Info()), Is.EqualTo(BannerFlags.None));
    }

    [Test]
    public void Flags_NotPlaced_WhenLiveVersionHasNoAssignment()
    {
        Banner banner = PublishedBanner(v => v.ZoneIds = []);

        Assert.That(_calculator.GetFlags(banner, Info()), Is.EqualTo(BannerFlags.NotPlaced));
    }

    [Test]
    public void Flags_NotPlaced_ForDraftBanner_UsesTheDraftVersion()
    {
        Banner placed = Banner(Version(BannerVersionKind.Draft, Now.UtcDateTime, 1), null, false, null);
        Banner notPlaced = Banner(Version(BannerVersionKind.Draft, Now.UtcDateTime), null, false, null);

        Assert.Multiple(() =>
        {
            Assert.That(_calculator.GetFlags(placed, Info()).HasFlag(BannerFlags.NotPlaced), Is.False);
            Assert.That(_calculator.GetFlags(notPlaced, Info()).HasFlag(BannerFlags.NotPlaced), Is.True);
        });
    }

    [Test]
    public void Flags_ImageMissing_WhenMediaGone()
    {
        Assert.That(_calculator.GetFlags(PublishedBanner(), Info(mediaExists: false)), Is.EqualTo(BannerFlags.ImageMissing));
    }

    [Test]
    public void Flags_ImageMissing_NeverForHtmlBanner()
    {
        Banner banner = PublishedBanner();
        banner.Type = BannerType.Html;

        Assert.That(_calculator.GetFlags(banner, Info(mediaExists: false)).HasFlag(BannerFlags.ImageMissing), Is.False);
    }

    [Test]
    public void Flags_Scheduled_WhenNowBeforeStart()
    {
        Banner banner = PublishedBanner(v => v.StartUtc = Now.UtcDateTime.AddSeconds(1));

        Assert.That(_calculator.GetFlags(banner, Info()), Is.EqualTo(BannerFlags.Scheduled));
    }

    [Test]
    public void Flags_NotScheduled_WhenNowEqualsStart()
    {
        Banner banner = PublishedBanner(v => v.StartUtc = Now.UtcDateTime);

        Assert.That(_calculator.GetFlags(banner, Info()), Is.EqualTo(BannerFlags.None));
    }

    [Test]
    public void Flags_Expired_WhenNowEqualsOrAfterEnd()
    {
        Banner atEnd = PublishedBanner(v => v.EndUtc = Now.UtcDateTime);
        Banner afterEnd = PublishedBanner(v => v.EndUtc = Now.UtcDateTime.AddSeconds(-1));
        Banner beforeEnd = PublishedBanner(v => v.EndUtc = Now.UtcDateTime.AddSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(_calculator.GetFlags(atEnd, Info()), Is.EqualTo(BannerFlags.Expired));
            Assert.That(_calculator.GetFlags(afterEnd, Info()), Is.EqualTo(BannerFlags.Expired));
            Assert.That(_calculator.GetFlags(beforeEnd, Info()), Is.EqualTo(BannerFlags.None));
        });
    }

    [Test]
    public void Flags_FollowTheClock()
    {
        Banner banner = PublishedBanner(v => v.EndUtc = Now.UtcDateTime.AddMinutes(10));
        Assert.That(_calculator.GetFlags(banner, Info()), Is.EqualTo(BannerFlags.None));

        _time.Advance(TimeSpan.FromMinutes(10));

        Assert.That(_calculator.GetFlags(banner, Info()), Is.EqualTo(BannerFlags.Expired));
    }

    [Test]
    public void Flags_LimitReached_ByImpressions()
    {
        Banner banner = PublishedBanner(v => v.ImpressionLimit = 100);

        Assert.Multiple(() =>
        {
            Assert.That(_calculator.GetFlags(banner, Info(impressions: 99)), Is.EqualTo(BannerFlags.None));
            Assert.That(_calculator.GetFlags(banner, Info(impressions: 100)), Is.EqualTo(BannerFlags.LimitReached));
            Assert.That(_calculator.GetFlags(banner, Info(impressions: 101)), Is.EqualTo(BannerFlags.LimitReached));
        });
    }

    [Test]
    public void Flags_LimitReached_ByClicks()
    {
        Banner banner = PublishedBanner(v => v.ClickLimit = 5);

        Assert.Multiple(() =>
        {
            Assert.That(_calculator.GetFlags(banner, Info(clicks: 4)), Is.EqualTo(BannerFlags.None));
            Assert.That(_calculator.GetFlags(banner, Info(clicks: 5)), Is.EqualTo(BannerFlags.LimitReached));
        });
    }

    [Test]
    public void Flags_NoLimitSet_NeverLimitReached()
    {
        Assert.That(_calculator.GetFlags(PublishedBanner(), Info(impressions: 1_000_000, clicks: 1_000_000)), Is.EqualTo(BannerFlags.None));
    }

    [Test]
    public void Flags_Combine()
    {
        Banner banner = PublishedBanner(v => { v.ZoneIds = []; v.EndUtc = Now.UtcDateTime.AddDays(-1); });

        BannerFlags flags = _calculator.GetFlags(banner, Info(mediaExists: false));

        Assert.That(flags, Is.EqualTo(BannerFlags.NotPlaced | BannerFlags.ImageMissing | BannerFlags.Expired));
    }
}
