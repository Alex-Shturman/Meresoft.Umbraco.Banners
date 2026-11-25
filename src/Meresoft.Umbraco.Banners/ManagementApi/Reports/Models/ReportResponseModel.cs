using Meresoft.Umbraco.Banners.Services.Models;

namespace Meresoft.Umbraco.Banners.ManagementApi.Reports.Models;

/// <summary>Totals of the report period.</summary>
public class ReportTotalsModel
{
    /// <summary>Viewable impressions.</summary>
    public long Impressions { get; set; }

    /// <summary>Clicks.</summary>
    public long Clicks { get; set; }

    /// <summary>Clicks per impression in percent with one decimal; null without impressions.</summary>
    public decimal? Ctr { get; set; }
}

/// <summary>The counts of one banner in one zone.</summary>
public class ReportRowModel
{
    /// <summary>Key of the banner; null when it was deleted.</summary>
    public Guid? BannerKey { get; set; }

    /// <summary>Banner name, or "(deleted banner)".</summary>
    public string BannerName { get; set; } = string.Empty;

    /// <summary>Zone alias, or "(deleted zone)".</summary>
    public string ZoneAlias { get; set; } = string.Empty;

    /// <summary>Viewable impressions.</summary>
    public long Impressions { get; set; }

    /// <summary>Clicks.</summary>
    public long Clicks { get; set; }

    /// <summary>Clicks per impression in percent with one decimal; null without impressions.</summary>
    public decimal? Ctr { get; set; }
}

/// <summary>The counts of one UTC day.</summary>
public class ReportDayModel
{
    /// <summary>The UTC day.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Viewable impressions.</summary>
    public long Impressions { get; set; }

    /// <summary>Clicks.</summary>
    public long Clicks { get; set; }

    /// <summary>True when part of the day came from the import service.</summary>
    public bool Imported { get; set; }
}

/// <summary>A statistics report.</summary>
public class ReportResponseModel
{
    /// <summary>Totals of the period.</summary>
    public ReportTotalsModel Totals { get; set; } = new();

    /// <summary>One row per banner per zone.</summary>
    public IReadOnlyList<ReportRowModel> Rows { get; set; } = [];

    /// <summary>One entry per day of the period.</summary>
    public IReadOnlyList<ReportDayModel> Daily { get; set; } = [];

    internal static ReportResponseModel From(BannerReport report) => new()
    {
        Totals = new ReportTotalsModel { Impressions = report.Totals.Impressions, Clicks = report.Totals.Clicks, Ctr = report.Totals.Ctr },
        Rows = report.Rows.Select(r => new ReportRowModel
        {
            BannerKey = r.BannerKey,
            BannerName = r.BannerName,
            ZoneAlias = r.ZoneAlias,
            Impressions = r.Impressions,
            Clicks = r.Clicks,
            Ctr = r.Ctr,
        }).ToList(),
        Daily = report.Daily.Select(d => new ReportDayModel { Day = d.Day, Impressions = d.Impressions, Clicks = d.Clicks, Imported = d.Imported }).ToList(),
    };
}
