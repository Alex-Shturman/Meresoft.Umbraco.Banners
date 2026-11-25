using System.Data.Common;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>
/// Writes the counter buffer to the database: by the flush job every interval and once more when the application stops.
/// One scope per row, so a failure on one row (for example two servers inserting the same new row at once) does not
/// roll back or lose the others. Rows that cannot be written are put back into the buffer for the next run.
/// </summary>
internal sealed class CounterFlusher
{
    private readonly CounterBuffer _buffer;
    private readonly IScopeProvider _scopeProvider;
    private readonly IStatisticsRepository _statistics;
    private readonly ILogger<CounterFlusher> _logger;

    public CounterFlusher(CounterBuffer buffer, IScopeProvider scopeProvider, IStatisticsRepository statistics, ILogger<CounterFlusher> logger)
    {
        _buffer = buffer;
        _scopeProvider = scopeProvider;
        _statistics = statistics;
        _logger = logger;
    }

    /// <summary>Takes the counts so far and adds them to the database.</summary>
    public void Flush()
    {
        IReadOnlyList<CounterEntry> entries = _buffer.Swap();
        if (entries.Count == 0)
        {
            return;
        }

        var failed = new List<CounterEntry>();
        foreach (CounterEntry entry in entries)
        {
            try
            {
                WriteOne(entry);
            }
            catch (DbException ex)
            {
                // A primary key violation from a racing server: the row exists now, so the second try is an update.
                _logger.LogDebug(ex, "Writing banner statistics of banner {BannerId} in zone {ZoneId} failed; trying once more.", entry.BannerId, entry.ZoneId);
                try
                {
                    WriteOne(entry);
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Writing banner statistics of banner {BannerId} in zone {ZoneId} failed twice; the counts are kept for the next run.", entry.BannerId, entry.ZoneId);
                    failed.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Writing banner statistics of banner {BannerId} in zone {ZoneId} failed; the counts are kept for the next run.", entry.BannerId, entry.ZoneId);
                failed.Add(entry);
            }
        }

        if (failed.Count > 0)
        {
            _buffer.Restore(failed);
        }
    }

    private void WriteOne(CounterEntry entry)
    {
        using IScope scope = _scopeProvider.CreateScope();
        _statistics.AddDelta(entry);
        scope.Complete();
    }
}
