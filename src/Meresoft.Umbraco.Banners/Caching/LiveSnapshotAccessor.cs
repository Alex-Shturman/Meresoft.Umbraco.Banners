using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Selection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Caching;

/// <inheritdoc cref="ILiveSnapshotAccessor" />
internal sealed class LiveSnapshotAccessor : ILiveSnapshotAccessor
{
    private readonly IScopeProvider _scopeProvider;
    private readonly IZoneRepository _zones;
    private readonly IBannerRepository _banners;
    private readonly IStatisticsRepository _statistics;
    private readonly LiveSnapshotBuilder _builder;
    private readonly ILogger<LiveSnapshotAccessor> _logger;

    // Rebuilds run one at a time so an older read can never replace a newer one; the lock also orders them against limit reloads.
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private volatile LiveSnapshot _current = LiveSnapshot.Empty;
    private volatile bool _built;
    private volatile bool _startupBuildFinished;
    private readonly TaskCompletionSource _buildSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _rebuildState = new();
    private bool _rebuildRunning;
    private TaskCompletionSource? _followUp;
    private HashSet<Guid>? _followUpMissing;
    private int _warnedBeforeFirstBuild;
    private int _notedStartupWindow;

    public LiveSnapshotAccessor(
        IScopeProvider scopeProvider,
        IZoneRepository zones,
        IBannerRepository banners,
        IStatisticsRepository statistics,
        LiveSnapshotBuilder builder,
        ILogger<LiveSnapshotAccessor> logger)
    {
        _scopeProvider = scopeProvider;
        _zones = zones;
        _banners = banners;
        _statistics = statistics;
        _builder = builder;
        _logger = logger;
    }

    public LiveSnapshot Current
    {
        get
        {
            if (!IsBuilt)
            {
                NoteReadBeforeFirstBuild();
            }

            return _current;
        }
    }

    public bool IsBuilt => _built;

    public void MarkStartupBuildFinished()
    {
        _startupBuildFinished = true;

        // Releases the waiters: when the build was given up, waiting longer would not help.
        _buildSignal.TrySetResult();
    }

    public async Task<bool> WaitForFirstBuildAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_built)
        {
            return true;
        }

        if (_startupBuildFinished)
        {
            return false;
        }

        try
        {
            await _buildSignal.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            // The bound was reached: the caller goes on without the snapshot.
        }

        return _built;
    }

    private void NoteReadBeforeFirstBuild()
    {
        // While the site is still starting, reads before the first build are normal. Once the start up build is over
        // without success, every zone looks "not set up" and no banner shows; that is worth one line at Warning,
        // because the cause (the build failed) is otherwise invisible on the page.
        if (_startupBuildFinished)
        {
            if (Interlocked.CompareExchange(ref _warnedBeforeFirstBuild, 1, 0) == 0)
            {
                _logger.LogWarning("The banner snapshot is read, but its build did not succeed at start up; no banner is shown until a refresh or the safety reload builds it.");
            }
        }
        else if (Interlocked.CompareExchange(ref _notedStartupWindow, 1, 0) == 0)
        {
            _logger.LogDebug("The banner snapshot is read before its start up build has finished.");
        }
    }

    public async Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null)
    {
        // Requests that arrive while a rebuild runs set a dirty flag (a shared follow-up) instead of each rebuilding: all of
        // them are answered by one more rebuild that starts after the running one, so it reads the latest data.
        Task? joined = null;
        lock (_rebuildState)
        {
            if (_rebuildRunning)
            {
                _followUp ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (mediaKeysTreatedAsMissing is { Count: > 0 })
                {
                    (_followUpMissing ??= []).UnionWith(mediaKeysTreatedAsMissing);
                }

                joined = _followUp.Task;
            }
            else
            {
                _rebuildRunning = true;
            }
        }

        if (joined is not null)
        {
            await joined;
            return;
        }

        IReadOnlySet<Guid>? missing = mediaKeysTreatedAsMissing;
        TaskCompletionSource? owed = null;
        while (true)
        {
            try
            {
                await BuildOnceAsync(missing);
                owed?.TrySetResult();
            }
            catch (Exception ex)
            {
                TaskCompletionSource? waiting;
                lock (_rebuildState)
                {
                    waiting = _followUp;
                    _followUp = null;
                    _followUpMissing = null;
                    _rebuildRunning = false;
                }

                owed?.TrySetException(ex);
                waiting?.TrySetException(ex);
                throw;
            }

            lock (_rebuildState)
            {
                if (_followUp is null)
                {
                    _rebuildRunning = false;
                    return;
                }

                owed = _followUp;
                missing = _followUpMissing;
                _followUp = null;
                _followUpMissing = null;
            }
        }
    }

    private async Task BuildOnceAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing)
    {
        await _rebuildLock.WaitAsync();
        try
        {
            IReadOnlyList<BannerZone> zones;
            IReadOnlyList<Banner> banners;
            IReadOnlyDictionary<int, BannerTotals> totals;
            using (IScope scope = _scopeProvider.CreateScope(autoComplete: true))
            {
                zones = _zones.GetAll();
                banners = _banners.GetAllPublished();

                // Only banners with a limit need their totals (summed over all zones and days, imported rows included).
                int[] limited = [.. banners
                    .Where(b => b.Live is { } live && (live.ImpressionLimit is not null || live.ClickLimit is not null))
                    .Select(b => b.Id)];
                totals = _statistics.GetTotals(limited);
            }

            _current = await _builder.BuildAsync(zones, banners, mediaKeysTreatedAsMissing, totals);
            _built = true;
            _buildSignal.TrySetResult();
            _logger.LogDebug("Banner snapshot rebuilt: {ZoneCount} zones, {MissingImageCount} banners with a missing image.", zones.Count, _current.ImageMissingBannerIds.Count);
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    public async Task ReloadLimitsAsync(CancellationToken cancellationToken = default)
    {
        // Behind the same lock as a rebuild, so a reload can never put an older snapshot over a newer one.
        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            LiveSnapshot current = _current;
            IReadOnlyCollection<int> limited = LimitFilter.LimitedBannerIds(current);
            if (limited.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<int, BannerTotals> totals;
            // Decision: the totals are read from the database every flush interval, not kept as a running counter here, so the
            // reload also sees the flushes of other servers and imported rows. The cost is one grouped query that grows with the
            // day rows (one per banner, zone and day) of the banners that have a limit; banners without a limit cost nothing.
            using (_scopeProvider.CreateScope(autoComplete: true))
            {
                totals = _statistics.GetTotals(limited);
            }

            LiveSnapshot next = LimitFilter.Apply(current, totals);
            if (!ReferenceEquals(next, current))
            {
                _current = next;
                _logger.LogDebug("Banner limit reload: {StoppedCount} banners reached their limit and left the snapshot.", limited.Count - LimitFilter.LimitedBannerIds(next).Count);
            }
        }
        finally
        {
            _rebuildLock.Release();
        }
    }
}
