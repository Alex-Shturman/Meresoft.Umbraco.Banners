namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Facts about a banner that are not part of its own data, needed for the runtime flags.</summary>
/// <param name="MediaExists">False when the image banner's media item is gone.</param>
/// <param name="TotalImpressions">Impressions over all zones and days, imported rows included.</param>
/// <param name="TotalClicks">Clicks over all zones and days, imported rows included.</param>
public readonly record struct BannerRuntimeInfo(bool MediaExists, long TotalImpressions, long TotalClicks);
