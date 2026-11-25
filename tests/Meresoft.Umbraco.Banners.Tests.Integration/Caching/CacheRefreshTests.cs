using System.Collections.Concurrent;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Caching;

/// <summary>Records every banner cache refresh message that was delivered on this server.</summary>
public sealed class RefreshMessageSpy : INotificationHandler<BannersCacheRefresherNotification>
{
    public static readonly ConcurrentQueue<BannersCacheRefresherPayload> Payloads = new();

    public void Handle(BannersCacheRefresherNotification notification)
    {
        foreach (BannersCacheRefresherPayload payload in (BannersCacheRefresherPayload[])notification.MessageObject)
        {
            Payloads.Enqueue(payload);
        }
    }
}

[TestFixture]
public class CacheRefreshTests : BannersServiceTestBase
{
    private BannerZone _zone = null!;
    private IMedia _media = null!;
    private Banner _banner = null!;

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.AddNotificationHandler<BannersCacheRefresherNotification, RefreshMessageSpy>();
    }

    [SetUp]
    public async Task Prepare()
    {
        RefreshMessageSpy.Payloads.Clear();
        _zone = await NewZoneAsync("home-top");
        _media = TestMedia.CreateImage(GetRequiredService<IMediaService>());
        _banner = await NewBannerAsync(ImageDraft(_media.Key, _zone.Key));
    }

    private ILiveSnapshotAccessor Accessor => GetRequiredService<ILiveSnapshotAccessor>();

    private async Task EventuallyAsync(Func<LiveSnapshot, bool> condition, string because)
    {
        DateTime stop = DateTime.UtcNow.AddSeconds(5);
        while (!condition(Accessor.Current) && DateTime.UtcNow < stop)
        {
            await Task.Delay(50);
        }

        Assert.That(condition(Accessor.Current), Is.True, $"within 5 seconds: {because}");
    }

    private static bool Shows(LiveSnapshot snapshot, int bannerId) =>
        snapshot.Zones.TryGetValue("home-top", out SnapshotZone? zone) && zone.Candidates.Any(c => c.BannerId == bannerId);

    [Test]
    public async Task Publish_SendsAPayload_AndTheSnapshotShowsTheBanner()
    {
        await BannerService.PublishAsync(_banner.Key, User);

        await EventuallyAsync(s => Shows(s, _banner.Id), "published banner is in the snapshot");
        Assert.That(RefreshMessageSpy.Payloads.Any(p => p.BannerId == _banner.Id), Is.True);
    }

    [Test]
    public async Task Publish_InsideAScopeThatRollsBack_SendsNoMessage_AndLeavesTheSnapshotUnchanged()
    {
        RefreshMessageSpy.Payloads.Clear();

        using (IScope outer = ScopeProvider.CreateScope())
        {
            var attempt = await BannerService.PublishAsync(_banner.Key, User);
            Assert.That(attempt.Success, Is.True);
            // The outer scope is disposed without Complete, so the publish is rolled back.
        }

        await Task.Delay(300);
        Assert.That(RefreshMessageSpy.Payloads, Is.Empty, "a message must only follow a commit");
        Assert.That(Shows(Accessor.Current, _banner.Id), Is.False);
        Assert.That((await BannerService.GetAsync(_banner.Key))!.IsPublished, Is.False);
    }

    [Test]
    public async Task Unpublish_RemovesTheBannerFromTheSnapshot()
    {
        await BannerService.PublishAsync(_banner.Key, User);
        await EventuallyAsync(s => Shows(s, _banner.Id), "shown");
        Clock.Advance(TimeSpan.FromMinutes(1));

        await BannerService.UnpublishAsync(_banner.Key, User);

        await EventuallyAsync(s => !Shows(s, _banner.Id), "unpublished banner is gone");
    }

    [Test]
    public async Task Delete_RemovesTheBannerFromTheSnapshot()
    {
        await BannerService.PublishAsync(_banner.Key, User);
        await EventuallyAsync(s => Shows(s, _banner.Id), "shown");

        await BannerService.DeleteAsync(_banner.Key, User);

        await EventuallyAsync(s => !Shows(s, _banner.Id), "deleted banner is gone");
    }

    [Test]
    public async Task ZoneChange_UpdatesTheSnapshot()
    {
        await BannerService.PublishAsync(_banner.Key, User);
        await EventuallyAsync(s => Shows(s, _banner.Id), "shown");
        Clock.Advance(TimeSpan.FromMinutes(1));

        var attempt = await ZoneService.UpdateAsync(
            _zone.Key,
            new BannerZoneModel { Alias = "renamed-zone", Name = "Renamed", ShowCount = 3, UpdateDate = _zone.UpdateDate },
            User);

        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        await EventuallyAsync(
            s => s.Zones.TryGetValue("renamed-zone", out SnapshotZone? z) && z.ShowCount == 3 && !s.Zones.ContainsKey("home-top"),
            "renamed zone is in the snapshot");
    }

    [Test]
    public async Task DeletingAReferencedMediaItem_DropsTheBanner_AndReportsImageMissing()
    {
        await BannerService.PublishAsync(_banner.Key, User);
        await EventuallyAsync(s => Shows(s, _banner.Id), "shown");

        GetRequiredService<IMediaService>().Delete(_media);
        // A real site binds media events to the distributed cache; the test host does not, so the
        // message Umbraco would send for a deleted media item is sent here.
        GetRequiredService<DistributedCache>().RefreshByPayload(
            MediaCacheRefresher.UniqueId,
            [new MediaCacheRefresher.JsonPayload(_media.Id, _media.Key, TreeChangeTypes.Remove)]);

        await EventuallyAsync(s => !Shows(s, _banner.Id) && s.ImageMissingBannerIds.Contains(_banner.Id), "banner dropped, image missing");
    }

    [Test]
    public async Task SafetyReload_RebuildsTheSnapshot_WhenNoMessageArrived()
    {
        await BannerService.PublishAsync(_banner.Key, User);
        await EventuallyAsync(s => Shows(s, _banner.Id), "shown");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            // A change made without any refresh message, as if a message got lost.
            GetRequiredService<IBannerRepository>().SetPublished(_banner.Id, false);
            scope.Complete();
        }

        Assert.That(Shows(Accessor.Current, _banner.Id), Is.True, "nothing refreshed the snapshot yet");
        await ActivatorUtilities.CreateInstance<SnapshotSafetyReloadJob>(Services).RunJobAsync(CancellationToken.None);

        Assert.That(Shows(Accessor.Current, _banner.Id), Is.False);
    }

    [Test]
    public void SafetyReload_UsesTheConfiguredInterval_AndIsOffWhenZero()
    {
        var on = ActivatorUtilities.CreateInstance<SnapshotSafetyReloadJob>(
            Services, Options.Create(new BannersOptions { CacheSafetyReloadInterval = TimeSpan.FromMinutes(7) }));
        var off = ActivatorUtilities.CreateInstance<SnapshotSafetyReloadJob>(
            Services, Options.Create(new BannersOptions { CacheSafetyReloadInterval = TimeSpan.Zero }));

        Assert.That(on.Period, Is.EqualTo(TimeSpan.FromMinutes(7)));
        Assert.That(off.Period, Is.EqualTo(Timeout.InfiniteTimeSpan));
        Assert.That(on.ServerRoles, Is.EquivalentTo(Enum.GetValues<ServerRole>()), "a subscriber server must heal a missed message too");
    }
}
