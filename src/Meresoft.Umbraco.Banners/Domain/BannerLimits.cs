namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>The one rule of a used up limit, shared by the snapshot filter and the back office flag.</summary>
internal static class BannerLimits
{
    /// <summary>
    /// True when the impression total reached the impression limit or the click total reached the click limit.
    /// A null limit never blocks.
    /// </summary>
    public static bool IsReached(long? impressionLimit, long? clickLimit, long impressions, long clicks) =>
        (impressionLimit is { } maxImpressions && impressions >= maxImpressions)
        || (clickLimit is { } maxClicks && clicks >= maxClicks);
}
