using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>US2 scenario 4 and clarification 1: one banner in several zones, drafts versus the live site.</summary>
[TestFixture]
public class MultiZoneAssignmentTests : BannersServiceTestBase
{
    private BannerZone _top = null!;
    private BannerZone _side = null!;

    [SetUp]
    public async Task Prepare()
    {
        _top = await NewZoneAsync("home-top");
        _side = await NewZoneAsync("home-side");
    }

    private ILiveSnapshotAccessor Accessor => GetRequiredService<ILiveSnapshotAccessor>();

    private IBannerSelector Selector => GetRequiredService<IBannerSelector>();

    private async Task<Banner> PublishedAsync(BannerDraftModel draft)
    {
        Banner banner = await NewBannerAsync(draft);
        var attempt = await BannerService.PublishAsync(banner.Key, User);
        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        await Accessor.RebuildAsync();
        return attempt.Result!;
    }

    private static SnapshotBanner? Candidate(LiveSnapshot snapshot, string alias, int bannerId) =>
        snapshot.Zones[alias].Candidates.SingleOrDefault(c => c.BannerId == bannerId);

    [Test]
    public async Task ABannerAssignedToTwoZones_IsACandidateInBoth_WithTheSameWeight()
    {
        BannerDraftModel draft = ImageDraft(NewMediaKey(), _top.Key, _side.Key);
        draft.Weight = 7;

        Banner banner = await PublishedAsync(draft);

        SnapshotBanner? inTop = Candidate(Accessor.Current, "home-top", banner.Id);
        SnapshotBanner? inSide = Candidate(Accessor.Current, "home-side", banner.Id);
        Assert.Multiple(() =>
        {
            Assert.That(inTop, Is.Not.Null);
            Assert.That(inSide, Is.Not.Null);
            Assert.That(inTop!.Weight, Is.EqualTo(7));
            Assert.That(inSide!.Weight, Is.EqualTo(7));
            Assert.That(inTop.ZoneId, Is.EqualTo(_top.Id));
            Assert.That(inSide!.ZoneId, Is.EqualTo(_side.Id));
            Assert.That(Selector.Select("home-top").Select(s => s.BannerId), Is.EqualTo(new[] { banner.Id }));
            Assert.That(Selector.Select("home-side").Select(s => s.BannerId), Is.EqualTo(new[] { banner.Id }));
        });
    }

    [Test]
    public async Task ChangingTheDraftsZones_DoesNotChangeTheSite_UntilPublish()
    {
        Banner banner = await PublishedAsync(ImageDraft(NewMediaKey(), _top.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));

        BannerDraftModel moved = ImageDraft(banner.Draft!.MediaKey!.Value, _side.Key);
        moved.UpdateDate = banner.UpdateDate;
        var saved = await BannerService.SaveDraftAsync(banner.Key, moved, User);
        Assert.That(saved.Success, Is.True, saved.Status.ToString());
        await Accessor.RebuildAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Candidate(Accessor.Current, "home-top", banner.Id), Is.Not.Null, "still live in the old zone");
            Assert.That(Candidate(Accessor.Current, "home-side", banner.Id), Is.Null, "the draft zone is not live yet");
        });

        Clock.Advance(TimeSpan.FromMinutes(1));
        var published = await BannerService.PublishAsync(banner.Key, User);
        Assert.That(published.Success, Is.True, published.Status.ToString());
        await Accessor.RebuildAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Candidate(Accessor.Current, "home-top", banner.Id), Is.Null);
            Assert.That(Candidate(Accessor.Current, "home-side", banner.Id), Is.Not.Null);
        });
    }

    [Test]
    public async Task RemovingAZoneFromABanner_KeepsTheDailyStatRowsOfThatZone()
    {
        Banner banner = await PublishedAsync(ImageDraft(NewMediaKey(), _top.Key, _side.Key));
        InsertStat(banner.Id, _side.Id, impressions: 40, clicks: 2);
        Clock.Advance(TimeSpan.FromMinutes(1));

        BannerDraftModel onlyTop = ImageDraft(banner.Draft!.MediaKey!.Value, _top.Key);
        onlyTop.UpdateDate = banner.UpdateDate;
        Assert.That((await BannerService.SaveDraftAsync(banner.Key, onlyTop, User)).Success, Is.True);
        Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.That((await BannerService.PublishAsync(banner.Key, User)).Success, Is.True);
        await Accessor.RebuildAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Candidate(Accessor.Current, "home-side", banner.Id), Is.Null);
            Assert.That(StatRows(banner.Id, _side.Id), Is.EqualTo((40L, 2L)));
        });
    }

    [Test]
    public async Task AnUnpublishedBanner_IsNeverSelected_AndKeepsItsStatistics()
    {
        Banner banner = await PublishedAsync(ImageDraft(NewMediaKey(), _top.Key));
        InsertStat(banner.Id, _top.Id, impressions: 12, clicks: 1);
        Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.That((await BannerService.UnpublishAsync(banner.Key, User)).Success, Is.True);
        await Accessor.RebuildAsync();

        Assert.Multiple(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                Assert.That(Selector.Select("home-top"), Is.Empty);
            }

            Assert.That(StatRows(banner.Id, _top.Id), Is.EqualTo((12L, 1L)));
        });
    }

    private void InsertStat(int bannerId, int zoneId, long impressions, long clicks)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        scope.Database.Insert(new DailyStatDto
        {
            Day = new DateOnly(2027, 3, 1),
            BannerId = bannerId,
            ZoneId = zoneId,
            Impressions = impressions,
            Clicks = clicks,
        });
    }

    private (long Impressions, long Clicks) StatRows(int bannerId, int zoneId)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        DailyStatDto row = scope.Database.Single<DailyStatDto>(
            "SELECT * FROM meresoftBannersDailyStat WHERE BannerId = @0 AND ZoneId = @1", bannerId, zoneId);
        return (row.Impressions, row.Clicks);
    }
}
