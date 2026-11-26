using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Rebuilds the snapshot every <see cref="BannersOptions.CacheSafetyReloadInterval"/>, in case a refresh message was missed. Off when the interval is zero.</summary>
internal sealed class SnapshotSafetyReloadJob : RecurringBackgroundJobBase
{
    private readonly ILiveSnapshotAccessor _accessor;
    private readonly ILogger<SnapshotSafetyReloadJob> _logger;
    private readonly bool _enabled;

    public SnapshotSafetyReloadJob(IOptions<BannersOptions> options, ILiveSnapshotAccessor accessor, ILogger<SnapshotSafetyReloadJob> logger)
        : base(options.Value.CacheSafetyReloadInterval > TimeSpan.Zero ? options.Value.CacheSafetyReloadInterval : Timeout.InfiniteTimeSpan)
    {
        _accessor = accessor;
        _logger = logger;
        _enabled = options.Value.CacheSafetyReloadInterval > TimeSpan.Zero;
    }

    /// <inheritdoc />
    public override ServerRole[] ServerRoles => Enum.GetValues<ServerRole>();

    public override async Task RunJobAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return;
        }

        try
        {
            await _accessor.RebuildAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The safety reload of the banner snapshot failed; it will try again at the next interval.");
        }
    }
}
