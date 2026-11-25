namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>Names shown for statistics whose banner or zone no longer exists (FR-007).</summary>
public static class ReportNames
{
    /// <summary>Name of a deleted banner.</summary>
    public const string DeletedBanner = "(deleted banner)";

    /// <summary>Alias shown for a deleted zone.</summary>
    public const string DeletedZone = "(deleted zone)";
}

/// <summary>Totals of a report period.</summary>
/// <param name="Impressions">Viewable impressions.</param>
/// <param name="Clicks">Clicks.</param>
/// <param name="Ctr">Clicks per impression in percent with one decimal; null without impressions.</param>
public sealed record ReportTotals(long Impressions, long Clicks, decimal? Ctr);

/// <summary>The counts of one banner in one zone over the period.</summary>
/// <param name="BannerKey">Key of the banner; null when it was deleted.</param>
/// <param name="BannerName">Banner name, or <see cref="ReportNames.DeletedBanner"/>.</param>
/// <param name="ZoneAlias">Zone alias, or <see cref="ReportNames.DeletedZone"/>.</param>
/// <param name="Impressions">Viewable impressions.</param>
/// <param name="Clicks">Clicks.</param>
/// <param name="Ctr">Click through rate in percent; null without impressions.</param>
public sealed record BannerReportRow(Guid? BannerKey, string BannerName, string ZoneAlias, long Impressions, long Clicks, decimal? Ctr);

/// <summary>The counts of one UTC day.</summary>
/// <param name="Day">The UTC day.</param>
/// <param name="Impressions">Viewable impressions.</param>
/// <param name="Clicks">Clicks.</param>
/// <param name="Imported">True when part of the day came from the import service.</param>
public sealed record ReportDay(DateOnly Day, long Impressions, long Clicks, bool Imported);

/// <summary>A statistics report: totals, one row per banner per zone and a series with every day of the period.</summary>
public sealed record BannerReport(ReportTotals Totals, IReadOnlyList<BannerReportRow> Rows, IReadOnlyList<ReportDay> Daily);

/// <summary>What a report covers.</summary>
public sealed class BannerReportQuery
{
    /// <summary>The longest period a report may cover, in days.</summary>
    public const int MaxDays = 366;

    /// <summary>First UTC day, inclusive.</summary>
    public DateOnly From { get; init; }

    /// <summary>Last UTC day, inclusive.</summary>
    public DateOnly To { get; init; }

    /// <summary>Only this zone, when set.</summary>
    public Guid? ZoneKey { get; init; }

    /// <summary>Only this banner, when set.</summary>
    public Guid? BannerKey { get; init; }
}
