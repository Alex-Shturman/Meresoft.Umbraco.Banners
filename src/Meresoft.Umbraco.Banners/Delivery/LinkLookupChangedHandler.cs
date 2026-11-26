using Meresoft.Umbraco.Banners.Caching;
using Umbraco.Cms.Core.Events;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>Ends the click lookup's memory of ids without a live link when a banner changes, so a banner published later is found at once.</summary>
internal sealed class LinkLookupChangedHandler : INotificationHandler<BannersChangedNotification>
{
    private readonly BannerLinkLookup _lookup;

    public LinkLookupChangedHandler(BannerLinkLookup lookup) => _lookup = lookup;

    public void Handle(BannersChangedNotification notification) => _lookup.ForgetMissing();
}
