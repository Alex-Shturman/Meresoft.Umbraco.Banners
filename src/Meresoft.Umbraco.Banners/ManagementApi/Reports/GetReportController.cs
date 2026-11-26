using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Reports.Models;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Reports;

/// <summary>The statistics report: totals, a row per banner per zone and a daily series.</summary>
public class GetReportController : BannersManagementApiControllerBase
{
    private readonly IBannerStatisticsService _statistics;

    /// <summary>Creates the controller.</summary>
    public GetReportController(IBannerStatisticsService statistics) => _statistics = statistics;

    /// <summary>Reports the counted impressions and clicks of a period of at most 366 UTC days.</summary>
    [HttpGet("reports")]
    [ProducesResponseType<ReportResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(DateOnly? from = null, DateOnly? to = null, Guid? zone = null, Guid? banner = null)
    {
        if (ReportQueries.Create(from, to, zone, banner, out BannerReportQuery? query) is { } problem)
        {
            return problem;
        }

        Attempt<BannerReport, BannerOperationStatus> attempt = await _statistics.GetReportAsync(query!);
        return attempt.Success
            ? Ok(ReportResponseModel.From(attempt.Result!))
            : ReportQueries.PeriodProblem();
    }
}
