namespace Meresoft.Umbraco.Banners.Statistics;

/// <summary>Click through rate: clicks divided by impressions, as a percent with one decimal.</summary>
public static class Ctr
{
    /// <summary>The rate, or null when there were no impressions (shown as a dash).</summary>
    public static decimal? Calculate(long impressions, long clicks) =>
        impressions <= 0 ? null : Math.Round(clicks * 100m / impressions, 1, MidpointRounding.AwayFromZero);
}
