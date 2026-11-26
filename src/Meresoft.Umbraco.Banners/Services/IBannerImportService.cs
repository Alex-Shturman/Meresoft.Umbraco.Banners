using Meresoft.Umbraco.Banners.Services.Models;

namespace Meresoft.Umbraco.Banners.Services;

/// <summary>
/// Brings statistics history from another system into the package. The package ships no importer for a specific CMS;
/// a site writes its own and feeds the rows here. Banners and zones are created with <see cref="IBannerService"/> and
/// <see cref="IBannerZoneService"/> first, so the rows can refer to their keys.
/// </summary>
public interface IBannerImportService
{
    /// <summary>
    /// Imports the rows in batches of 1,000, one transaction per batch. Imported rows are marked as imported in reports.
    /// Rows with an unknown banner or zone key or a negative count are skipped and counted as rejected. If the database
    /// fails the exception is thrown; batches that were already committed stay.
    /// After a <see cref="StatisticImportMode.Replace"/> import the live banners are rebuilt, so a banner whose totals went
    /// down below its limit shows again.
    /// </summary>
    Task<BannerImportResult> ImportStatisticsAsync(IEnumerable<DailyStatisticImport> rows, StatisticImportMode mode);
}
