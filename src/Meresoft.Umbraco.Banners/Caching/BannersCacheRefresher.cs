using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Serialization;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// Distributes "banners changed" messages to all servers. Each server then rebuilds its live
/// snapshot through <see cref="SnapshotRebuildHandler"/>.
/// </summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public sealed class BannersCacheRefresher : PayloadCacheRefresherBase<BannersCacheRefresherNotification, BannersCacheRefresherPayload>
{
    /// <summary>Fixed, unique id of this refresher.</summary>
    public static readonly Guid UniqueId = Guid.Parse("6f0d4c7e-3b52-4a8e-9c1f-5d2a8b7e4c10");

    /// <summary>Creates the refresher.</summary>
    public BannersCacheRefresher(
        AppCaches appCaches,
        IJsonSerializer serializer,
        IEventAggregator eventAggregator,
        ICacheRefresherNotificationFactory factory)
        : base(appCaches, serializer, eventAggregator, factory)
    {
    }

    /// <inheritdoc />
    public override Guid RefresherUniqueId => UniqueId;

    /// <inheritdoc />
    public override string Name => "Meresoft Banners Cache Refresher";
}
