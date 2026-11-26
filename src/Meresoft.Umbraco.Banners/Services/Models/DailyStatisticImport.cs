namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>One day of counts of a banner in a zone, as brought in from another system.</summary>
public class DailyStatisticImport
{
    /// <summary>The day, in UTC like all package statistics.</summary>
    public DateOnly Day { get; set; }

    /// <summary>Key of the banner the counts belong to.</summary>
    public Guid BannerKey { get; set; }

    /// <summary>Key of the zone the counts belong to.</summary>
    public Guid ZoneKey { get; set; }

    /// <summary>Impressions of the day; not negative.</summary>
    public long Impressions { get; set; }

    /// <summary>Clicks of the day; not negative.</summary>
    public long Clicks { get; set; }
}
