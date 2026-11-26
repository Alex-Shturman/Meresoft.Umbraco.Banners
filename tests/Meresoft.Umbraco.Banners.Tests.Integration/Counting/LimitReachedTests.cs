using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>User story 5: a banner stops by itself after its limit, and shows again when the limit is raised.</summary>
[TestFixture]
public class LimitReachedTests : BannersServiceTestBase
{
    private static readonly DateOnly Day = new(2027, 3, 1);

    private BannerZone _top = null!;
    private BannerZone _side = null!;
    private Banner _banner = null!;

    private CounterBuffer Buffer => GetRequiredService<CounterBuffer>();

    private ILiveSnapshotAccessor Snapshot => GetRequiredService<ILiveSnapshotAccessor>();

    [SetUp]
    public async Task Prepare()
    {
        _top = await NewZoneAsync("limit-top");
        _side = await NewZoneAsync("limit-side");
        _banner = await PublishLimitedAsync(limit: 3);
    }

    private async Task<Banner> PublishLimitedAsync(long? limit, long? clickLimit = null)
    {
        BannerDraftModel draft = ImageDraft(NewMediaKey(), _top.Key, _side.Key);
        draft.ImpressionLimit = limit;
        draft.ClickLimit = clickLimit;
        Banner banner = await NewBannerAsync(draft);
        var published = await BannerService.PublishAsync(banner.Key, User);
        Assert.That(published.Success, Is.True, published.Status.ToString());
        await Snapshot.RebuildAsync();
        return banner;
    }

    private static bool Shows(LiveSnapshot snapshot, string alias, int bannerId) =>
        snapshot.Zones[alias].Candidates.Any(c => c.BannerId == bannerId);

    private Task FlushAsync() => ActivatorUtilities.CreateInstance<BannersFlushJob>(Services).RunJobAsync(CancellationToken.None);

    private async Task<BannerFlags> FlagsAsync(Banner banner) => (await BannerService.GetAsync(banner.Key))!.Flags;

    [Test]
    public async Task FiveViewsAcrossTwoZones_AfterTheFlush_TheBannerIsGone_AndTheListFlagSaysLimitReached()
    {
        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.True, "shows before the limit");
        Assert.That(await FlagsAsync(_banner), Is.EqualTo(BannerFlags.None));

        for (int i = 0; i < 3; i++)
        {
            Buffer.AddImpression(Day, _banner.Id, _top.Id);
        }

        for (int i = 0; i < 2; i++)
        {
            Buffer.AddImpression(Day, _banner.Id, _side.Id);
        }

        await FlushAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.False);
        Assert.That(Shows(Snapshot.Current, "limit-side", _banner.Id), Is.False);
        Assert.That((await FlagsAsync(_banner)).HasFlag(BannerFlags.LimitReached), Is.True);
    }

    [Test]
    public async Task BelowTheLimit_AfterTheFlush_TheBannerStaysAndHasNoFlag()
    {
        Buffer.AddImpression(Day, _banner.Id, _top.Id);
        Buffer.AddImpression(Day, _banner.Id, _side.Id);

        await FlushAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.True);
        Assert.That(await FlagsAsync(_banner), Is.EqualTo(BannerFlags.None));
    }

    [Test]
    public async Task TheLimitCountsTheTotalOverAllZones_NotThePerZoneCount()
    {
        // Two views in each zone: no zone reaches 3, the banner's total of 4 does.
        Buffer.AddImpression(Day, _banner.Id, _top.Id);
        Buffer.AddImpression(Day, _banner.Id, _top.Id);
        Buffer.AddImpression(Day, _banner.Id, _side.Id);
        Buffer.AddImpression(Day, _banner.Id, _side.Id);

        await FlushAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.False);
    }

    [Test]
    public async Task ImportedRows_CountTowardsTheLimit_AfterARebuild()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new DailyStatDto { Day = Day, BannerId = _banner.Id, ZoneId = _top.Id, Impressions = 3, Clicks = 0, IsImported = true });
            scope.Complete();
        }

        await Snapshot.RebuildAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.False);
        Assert.That((await FlagsAsync(_banner)).HasFlag(BannerFlags.LimitReached), Is.True);
    }

    [Test]
    public async Task TheClickLimit_StopsTheBanner_Too()
    {
        Banner clickLimited = await PublishLimitedAsync(limit: null, clickLimit: 2);
        Buffer.AddImpression(Day, clickLimited.Id, _top.Id);
        Buffer.AddClick(Day, clickLimited.Id, _top.Id);
        await FlushAsync();
        Assert.That(Shows(Snapshot.Current, "limit-top", clickLimited.Id), Is.True, "one click of two");

        Buffer.AddClick(Day, clickLimited.Id, _side.Id);
        await FlushAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", clickLimited.Id), Is.False);
    }

    [Test]
    public async Task RaisingTheLimitAndPublishing_ShowsTheBannerAgain()
    {
        for (int i = 0; i < 4; i++)
        {
            Buffer.AddImpression(Day, _banner.Id, _top.Id);
        }

        await FlushAsync();
        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.False);

        BannerDraftModel raised = ImageDraft(NewMediaKey(), _top.Key, _side.Key);
        raised.ImpressionLimit = 10;
        Assert.That((await BannerService.SaveDraftAsync(_banner.Key, raised, User)).Success, Is.True);
        Assert.That((await BannerService.PublishAsync(_banner.Key, User)).Success, Is.True);
        await Snapshot.RebuildAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", _banner.Id), Is.True);
        Assert.That((await FlagsAsync(_banner)).HasFlag(BannerFlags.LimitReached), Is.False);
    }

    private BannerDraftModel HtmlDraft(long? impressionLimit, long? clickLimit) => new()
    {
        Name = "Html offer",
        Html = "<p>Invented html offer</p>",
        Weight = 1,
        ImpressionLimit = impressionLimit,
        ClickLimit = clickLimit,
        ZoneKeys = [_top.Key],
    };

    [Test]
    public async Task AnHtmlBannerWithAClickLimit_IsRejected_BecauseItsClicksAreNotCounted()
    {
        var attempt = await BannerService.CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = HtmlDraft(null, clickLimit: 5) }, User);

        Assert.That(attempt.Success, Is.False);
        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
    }

    [Test]
    public async Task AnHtmlBannerWithAnImpressionLimit_LeavesTheSnapshotAtTheLimit_LikeAnImageBanner()
    {
        var created = await BannerService.CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = HtmlDraft(impressionLimit: 2, clickLimit: null) }, User);
        Assert.That(created.Success, Is.True, created.Status.ToString());
        Assert.That((await BannerService.PublishAsync(created.Result!.Key, User)).Success, Is.True);
        await Snapshot.RebuildAsync();
        int id = created.Result.Id;
        Assert.That(Shows(Snapshot.Current, "limit-top", id), Is.True);

        Buffer.AddImpression(Day, id, _top.Id);
        await FlushAsync();
        Assert.That(Shows(Snapshot.Current, "limit-top", id), Is.True, "one view of two");

        Buffer.AddImpression(Day, id, _side.Id);
        await FlushAsync();

        Assert.That(Shows(Snapshot.Current, "limit-top", id), Is.False);
        Assert.That((await FlagsAsync(created.Result)).HasFlag(BannerFlags.LimitReached), Is.True);
    }

    /// <summary>One page request per call: the real selector picks, and every served banner counts one view, as the page script would.</summary>
    private int ServeRequests(int requests)
    {
        IBannerSelector selector = GetRequiredService<IBannerSelector>();
        int counted = 0;
        for (int i = 0; i < requests; i++)
        {
            foreach (SelectedBanner served in selector.Select("limit-top"))
            {
                if (served.BannerId == _banner.Id)
                {
                    Buffer.AddImpression(Day, _banner.Id, _top.Id);
                    counted++;
                }
            }
        }

        return counted;
    }

    private long StoredImpressions()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return GetRequiredService<IStatisticsRepository>().GetDailyRows(new DateOnly(2000, 1, 1), new DateOnly(2100, 1, 1), null, _banner.Id).Sum(r => r.Impressions);
    }

    [Test]
    public async Task TheOvershoot_IsAtMostTheViewsOfOneFlushInterval_AndNothingIsCountedAfterIt()
    {
        // Interval 1: below the limit of 3, so the banner keeps being served.
        Assert.That(ServeRequests(1), Is.EqualTo(1));
        await FlushAsync();

        // Interval 2 crosses the limit. The banner is still served during the whole interval (the snapshot only
        // changes at the flush), so every request counts: this is the allowed overshoot.
        const int requestsInTheCrossingInterval = 4;
        Assert.That(ServeRequests(requestsInTheCrossingInterval), Is.EqualTo(requestsInTheCrossingInterval));
        await FlushAsync();

        // Interval 3: the selection path no longer serves the banner, so nothing is recorded.
        Assert.That(GetRequiredService<IBannerSelector>().Select("limit-top"), Is.Empty);
        Assert.That(GetRequiredService<IBannerSelector>().Select("limit-side"), Is.Empty);
        Assert.That(ServeRequests(requestsInTheCrossingInterval), Is.EqualTo(0));
        await FlushAsync();

        long total = StoredImpressions();
        Assert.That(total, Is.EqualTo(1 + requestsInTheCrossingInterval), "views past the limit exist only in the crossing interval");
        Assert.That(total - 3, Is.LessThanOrEqualTo(requestsInTheCrossingInterval), "overshoot is at most the views of one interval");
    }
}
