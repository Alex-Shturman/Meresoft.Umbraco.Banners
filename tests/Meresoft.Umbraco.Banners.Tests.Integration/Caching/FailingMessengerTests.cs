using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Caching;

/// <summary>A refresh message that cannot be sent must not fail the save or publish that is already committed (the safety reload heals it).</summary>
[TestFixture]
public class FailingMessengerTests : BannersServiceTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IServerMessenger, ThrowingServerMessenger>();
    }

    [Test]
    public async Task Publish_StillReturnsSuccess_AndIsCommitted_WhenTheMessageCannotBeSent()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        Guid media = TestMedia.CreateImage(GetRequiredService<IMediaService>()).Key;
        Banner banner = await NewBannerAsync(ImageDraft(media, zone.Key));

        var attempt = await BannerService.PublishAsync(banner.Key, User);

        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        Assert.That((await BannerService.GetAsync(banner.Key))!.IsPublished, Is.True);
    }

    [Test]
    public async Task ZoneCreate_StillReturnsSuccess_WhenTheMessageCannotBeSent()
    {
        var attempt = await ZoneService.CreateAsync(new BannerZoneModel { Alias = "home-top", Name = "Home", ShowCount = 1 }, User);

        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        Assert.That(await ZoneService.GetByAliasAsync("home-top"), Is.Not.Null);
    }

    private sealed class ThrowingServerMessenger : IServerMessenger
    {
        public void QueueRefresh<TPayload>(ICacheRefresher refresher, TPayload[] payload) =>
            throw new InvalidOperationException("The messenger is down.");

        public void Sync() { }
        public void SendMessages() { }
        public void QueueRefresh<T>(ICacheRefresher refresher, Func<T, int> getNumericId, params T[] instances) { }
        public void QueueRefresh<T>(ICacheRefresher refresher, Func<T, Guid> getGuidId, params T[] instances) { }
        public void QueueRemove<T>(ICacheRefresher refresher, Func<T, int> getNumericId, params T[] instances) { }
        public void QueueRemove(ICacheRefresher refresher, params int[] numericIds) { }
        public void QueueRefresh(ICacheRefresher refresher, params int[] numericIds) { }
        public void QueueRefresh(ICacheRefresher refresher, params Guid[] guidIds) { }
        public void QueueRefreshAll(ICacheRefresher refresher) { }
    }
}
