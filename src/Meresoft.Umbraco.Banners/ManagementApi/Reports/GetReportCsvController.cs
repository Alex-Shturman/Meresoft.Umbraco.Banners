using System.Globalization;
using System.Text;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Reports;

/// <summary>The same report as a CSV file, one line per banner per zone per day.</summary>
public class GetReportCsvController : BannersManagementApiControllerBase
{
    private const string ContentType = "text/csv; charset=utf-8";

    private readonly IBannerStatisticsService _statistics;

    /// <summary>Creates the controller.</summary>
    public GetReportCsvController(IBannerStatisticsService statistics) => _statistics = statistics;

    /// <summary>Downloads the report as <c>banners-{from}-{to}.csv</c>.</summary>
    [HttpGet("reports/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(DateOnly? from = null, DateOnly? to = null, Guid? zone = null, Guid? banner = null)
    {
        if (ReportQueries.Create(from, to, zone, banner, out BannerReportQuery? query) is { } problem)
        {
            return problem;
        }

        Attempt<string, BannerOperationStatus> attempt = await _statistics.GetCsvAsync(query!);
        if (!attempt.Success)
        {
            return ReportQueries.PeriodProblem();
        }

        string fileName = string.Create(CultureInfo.InvariantCulture, $"banners-{query!.From:yyyy-MM-dd}-{query.To:yyyy-MM-dd}.csv");
        return File(Encoding.UTF8.GetBytes(attempt.Result!), ContentType, fileName);
    }
}
