using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Builds the first snapshot once the site has started, so the first page request already finds it.</summary>
internal sealed class SnapshotStartupHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly ILiveSnapshotAccessor _accessor;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<SnapshotStartupHandler> _logger;

    public SnapshotStartupHandler(ILiveSnapshotAccessor accessor, IRuntimeState runtimeState, ILogger<SnapshotStartupHandler> logger)
    {
        _accessor = accessor;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        try
        {
            await _accessor.RebuildAsync();
        }
        catch (Exception ex)
        {
            // The package tables may not exist yet on the first start; the safety reload builds the snapshot later.
            _logger.LogWarning(ex, "The first banner snapshot could not be built; the safety reload will retry.");
        }
    }
}
