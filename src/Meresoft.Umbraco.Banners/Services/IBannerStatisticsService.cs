using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.Services;

/// <summary>Reads the counted impressions and clicks. Only flushed counts are included, so they can be about a minute behind.</summary>
public interface IBannerStatisticsService
{
    /// <summary>The report for the period. <see cref="BannerOperationStatus.InvalidData"/> when the end is before the start or the period is longer than 366 days.</summary>
    Task<Attempt<BannerReport, BannerOperationStatus>> GetReportAsync(BannerReportQuery query);

    /// <summary>The same period as CSV text, one line per banner per zone per day.</summary>
    Task<Attempt<string, BannerOperationStatus>> GetCsvAsync(BannerReportQuery query);
}
