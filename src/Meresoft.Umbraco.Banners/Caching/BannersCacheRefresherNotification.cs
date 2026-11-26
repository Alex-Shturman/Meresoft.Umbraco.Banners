using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Published on every server when a banner cache refresh message arrives.</summary>
public sealed class BannersCacheRefresherNotification : CacheRefresherNotification
{
    /// <summary>Creates the notification.</summary>
    public BannersCacheRefresherNotification(object messageObject, MessageType messageType)
        : base(messageObject, messageType)
    {
    }
}
