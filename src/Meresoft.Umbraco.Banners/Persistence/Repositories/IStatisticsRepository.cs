using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>One stored statistics row.</summary>
internal readonly record struct DailyStatRow(DateOnly Day, int BannerId, int ZoneId, long Impressions, long Clicks, bool IsImported);

/// <summary>Reads and adds to the daily statistics. Methods use the ambient Umbraco scope.</summary>
internal interface IStatisticsRepository
{
    /// <summary>Adds the counts to the row of the day, banner and zone, creating the row when it does not exist.</summary>
    void AddDelta(CounterEntry delta);

    /// <summary>The rows of the period (both days included), optionally for one zone or one banner.</summary>
    IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId);

    /// <summary>Totals over all zones and days (imported rows included) of the banners that have any statistics.</summary>
    IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds);
}
