using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// Rebuilds the snapshot when a media item that a published banner uses changes or is deleted, so
/// the image address stays right and a deleted image takes its banner out of rotation ("image missing").
/// </summary>
internal sealed class MediaChangedHandler : INotificationAsyncHandler<MediaCacheRefresherNotification>
{
    private readonly ILiveSnapshotAccessor _accessor;

    public MediaChangedHandler(ILiveSnapshotAccessor accessor) => _accessor = accessor;

    public Task HandleAsync(MediaCacheRefresherNotification notification, CancellationToken cancellationToken)
    {
        if (notification.MessageObject is not MediaCacheRefresher.JsonPayload[] payloads)
        {
            return Task.CompletedTask;
        }

        IReadOnlySet<Guid> used = _accessor.Current.MediaKeys;
        bool referenced = payloads.Any(p => p.Key is { } key && used.Contains(key));
        return referenced ? _accessor.RebuildAsync() : Task.CompletedTask;
    }
}
