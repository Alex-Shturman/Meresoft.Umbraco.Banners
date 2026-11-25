using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Caching;

/// <summary>
/// Umbraco's batched database messenger does not send inside QueueRefresh: it enlists its own instruction batch in the
/// scope context and sends when the scope completes. The package must queue its message in a way that still reaches
/// such a messenger, so this messenger does the same.
/// This is a regression guard. It does not fail on the earlier implementation that enlisted in the scope context
/// itself: that implementation also reached this messenger. It protects the notification pattern that replaced it.
/// </summary>
[TestFixture]
public class EnlistingMessengerTests : BannersServiceTestBase
{
    public static int Delivered;

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IServerMessenger, EnlistingServerMessenger>();
    }

    [SetUp]
    public void ResetCounter() => Delivered = 0;

    [Test]
    public async Task APublish_ReachesAMessengerThatEnlistsInsideQueueRefresh_AfterTheCommit()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        Guid media = TestMedia.CreateImage(GetRequiredService<IMediaService>()).Key;
        Banner banner = await NewBannerAsync(ImageDraft(media, zone.Key));
        Delivered = 0;

        var attempt = await BannerService.PublishAsync(banner.Key, User);

        Assert.That(attempt.Success, Is.True);
        ILiveSnapshotAccessor accessor = GetRequiredService<ILiveSnapshotAccessor>();
        DateTime stop = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < stop && !(accessor.Current.Zones.TryGetValue("home-top", out SnapshotZone? z) && z.Candidates.Count == 1))
        {
            await Task.Delay(50);
        }

        Assert.That(Delivered, Is.GreaterThan(0), "the message never reached the messenger");
        Assert.That(accessor.Current.Zones["home-top"].Candidates, Has.Count.EqualTo(1));
    }

    private sealed class EnlistingServerMessenger : IServerMessenger
    {
        private readonly ICoreScopeProvider _scopeProvider;
        private readonly IJsonSerializer _serializer;

        public EnlistingServerMessenger(ICoreScopeProvider scopeProvider, IJsonSerializer serializer)
        {
            _scopeProvider = scopeProvider;
            _serializer = serializer;
        }

        public void QueueRefresh<TPayload>(ICacheRefresher refresher, TPayload[] payload)
        {
            void Deliver()
            {
                Interlocked.Increment(ref Delivered);
                if (refresher is IJsonCacheRefresher json)
                {
                    json.Refresh(_serializer.Serialize(payload));
                }
            }

            IScopeContext? context = _scopeProvider.Context;
            if (context is null)
            {
                Deliver();
                return;
            }

            context.Enlist(Guid.NewGuid().ToString(), () => 0, (completed, _) =>
            {
                if (completed)
                {
                    Deliver();
                }
            });
        }

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
