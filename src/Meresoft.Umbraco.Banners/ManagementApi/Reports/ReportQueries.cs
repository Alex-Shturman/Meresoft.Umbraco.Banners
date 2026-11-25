using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Reports;

/// <summary>Turns the query string of the report endpoints into a query, or into the problem answer.</summary>
internal static class ReportQueries
{
    /// <summary>Null when the query is usable and <paramref name="query"/> is set; otherwise the 400 answer.</summary>
    public static IActionResult? Create(DateOnly? from, DateOnly? to, Guid? zone, Guid? banner, out BannerReportQuery? query)
    {
        query = null;
        if (from is null || to is null)
        {
            return OperationStatusResults.ToProblem(
                BannerOperationStatus.InvalidData,
                new Dictionary<string, string> { [from is null ? "from" : "to"] = "Give the period as UTC dates, for example 2027-03-01." });
        }

        query = new BannerReportQuery { From = from.Value, To = to.Value, ZoneKey = zone, BannerKey = banner };
        return null;
    }

    /// <summary>The 400 answer for an end before the start or a period of more than 366 days.</summary>
    public static IActionResult PeriodProblem() =>
        OperationStatusResults.ToProblem(
            BannerOperationStatus.InvalidData,
            new Dictionary<string, string> { ["to"] = $"The period must end on or after its start and cover at most {BannerReportQuery.MaxDays} days." });
}
