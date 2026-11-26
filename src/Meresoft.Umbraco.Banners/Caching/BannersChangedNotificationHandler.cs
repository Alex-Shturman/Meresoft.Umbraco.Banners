using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Sends the refresh message once the change is committed.</summary>
internal sealed class BannersChangedNotificationHandler : INotificationHandler<BannersChangedNotification>
{
    private readonly DistributedCache _distributedCache;
    private readonly ILogger<BannersChangedNotificationHandler> _logger;

    public BannersChangedNotificationHandler(DistributedCache distributedCache, ILogger<BannersChangedNotificationHandler> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public void Handle(BannersChangedNotification notification)
    {
        try
        {
            _distributedCache.RefreshBanners(notification.Payload);
        }
        catch (Exception ex)
        {
            // The change is already committed, so the editor's save or publish must not fail because a message could
            // not be sent. Servers that missed it catch up at the next refresh or at the safety reload.
            _logger.LogError(
                ex,
                "The banner refresh message could not be sent (banner id {BannerId}, zone id {ZoneId}); servers catch up at the safety reload.",
                notification.Payload.BannerId,
                notification.Payload.ZoneId);
        }
    }
}
