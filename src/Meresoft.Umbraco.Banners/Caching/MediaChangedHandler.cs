using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Changes;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// Rebuilds the snapshot when a media item that a published banner uses changes or is deleted, so
/// the image address stays right and a deleted image takes its banner out of rotation ("image missing").
/// </summary>
internal sealed class MediaChangedHandler : INotificationAsyncHandler<MediaCacheRefresherNotification>
{
    private readonly ILiveSnapshotAccessor _accessor;
    private readonly IScopeProvider _scopeProvider;
    private readonly IMediaService _mediaService;

    public MediaChangedHandler(ILiveSnapshotAccessor accessor, IScopeProvider scopeProvider, IMediaService mediaService)
    {
        _accessor = accessor;
        _scopeProvider = scopeProvider;
        _mediaService = mediaService;
    }

    public Task HandleAsync(MediaCacheRefresherNotification notification, CancellationToken cancellationToken)
    {
        if (notification.MessageObject is not MediaCacheRefresher.JsonPayload[] payloads)
        {
            return Task.CompletedTask;
        }

        IReadOnlySet<Guid> used = _accessor.Current.MediaKeys;
        if (used.Count == 0)
        {
            return Task.CompletedTask;
        }

        const TreeChangeTypes removing = TreeChangeTypes.Remove | TreeChangeTypes.RefreshBranch | TreeChangeTypes.RefreshAll;
        bool removal = payloads.Any(p => (p.ChangeTypes & removing) != 0);
        if (!removal)
        {
            return payloads.Any(p => p.Key is { } key && used.Contains(key)) ? _accessor.RebuildAsync() : Task.CompletedTask;
        }

        // Deleting or moving a folder sends only the folder key, and the media cache may not have caught up when
        // this handler runs. So every media item a banner uses is checked in the database, and the ones that are gone
        // (or in the recycle bin) are handed to the rebuild as missing.
        var gone = new HashSet<Guid>(payloads.Where(p => (p.ChangeTypes & TreeChangeTypes.Remove) != 0 && p.Key is not null).Select(p => p.Key!.Value));
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            foreach (Guid key in used)
            {
                IMedia? media = _mediaService.GetById(key);
                if (media is null || media.Trashed)
                {
                    gone.Add(key);
                }
            }
        }

        return _accessor.RebuildAsync(gone);
    }
}
