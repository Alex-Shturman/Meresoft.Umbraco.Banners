using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Sends the refresh message once the change is committed.</summary>
internal sealed class BannersChangedNotificationHandler : INotificationHandler<BannersChangedNotification>
{
    private readonly DistributedCache _distributedCache;

    public BannersChangedNotificationHandler(DistributedCache distributedCache) => _distributedCache = distributedCache;

    public void Handle(BannersChangedNotification notification) => _distributedCache.RefreshBanners(notification.Payload);
}
