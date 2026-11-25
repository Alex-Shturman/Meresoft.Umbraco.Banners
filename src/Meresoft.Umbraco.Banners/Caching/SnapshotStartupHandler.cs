using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Builds the first snapshot once the site has started, so the first page request already finds it.</summary>
internal sealed class SnapshotStartupHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromSeconds(1);

    private readonly ILiveSnapshotAccessor _accessor;
    private readonly IRuntimeState _runtimeState;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SnapshotStartupHandler> _logger;

    public SnapshotStartupHandler(ILiveSnapshotAccessor accessor, IRuntimeState runtimeState, TimeProvider timeProvider, ILogger<SnapshotStartupHandler> logger)
    {
        _accessor = accessor;
        _runtimeState = runtimeState;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        TimeSpan backoff = FirstBackoff;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await _accessor.RebuildAsync();
                return;
            }
            catch (OperationCanceledException)
            {
                throw; // The site is stopping; that is not a failure to report.
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "The first banner snapshot could not be built (try {Attempt} of {MaxAttempts}); trying again in {Backoff}.", attempt, MaxAttempts, backoff);
                await Task.Delay(backoff, _timeProvider, cancellationToken);
                backoff *= 2;
            }
            catch (Exception ex)
            {
                // Startup must go on. Zones that exist show nothing and are listed as "placed but not set up"
                // until a publish, a zone change or the safety reload builds the snapshot.
                _logger.LogError(ex, "The first banner snapshot could not be built after {MaxAttempts} tries; banners stay hidden until the next refresh or safety reload.", MaxAttempts);
                return;
            }
        }
    }
}
