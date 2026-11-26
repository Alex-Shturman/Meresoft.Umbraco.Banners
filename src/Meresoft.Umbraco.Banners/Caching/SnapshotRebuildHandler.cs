using Umbraco.Cms.Core.Events;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Rebuilds the snapshot when a banner refresh message arrives (publish, unpublish, delete, zone change).</summary>
internal sealed class SnapshotRebuildHandler : INotificationAsyncHandler<BannersCacheRefresherNotification>
{
    private readonly ILiveSnapshotAccessor _accessor;

    public SnapshotRebuildHandler(ILiveSnapshotAccessor accessor) => _accessor = accessor;

    public Task HandleAsync(BannersCacheRefresherNotification notification, CancellationToken cancellationToken) =>
        _accessor.RebuildAsync();
}
