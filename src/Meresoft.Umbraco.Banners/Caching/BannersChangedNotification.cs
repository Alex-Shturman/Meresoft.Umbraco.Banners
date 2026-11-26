using Umbraco.Cms.Core.Notifications;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// Published through the scope's notification publisher, which dispatches it only after the scope has committed
/// (and drops it on a rollback). Its handler sends the refresh message to all servers.
/// </summary>
internal sealed class BannersChangedNotification : INotification
{
    public BannersChangedNotification(BannersCacheRefresherPayload payload) => Payload = payload;

    public BannersCacheRefresherPayload Payload { get; }
}
