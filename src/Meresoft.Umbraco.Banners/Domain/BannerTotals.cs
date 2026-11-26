namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Impressions and clicks of a banner summed over all zones and days.</summary>
public readonly record struct BannerTotals(long Impressions, long Clicks);
