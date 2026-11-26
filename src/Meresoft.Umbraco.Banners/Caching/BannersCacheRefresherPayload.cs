namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>What changed, sent to every server so it rebuilds its snapshot.</summary>
public sealed class BannersCacheRefresherPayload
{
    /// <summary>Internal id of the banner that changed, if a banner changed.</summary>
    public int? BannerId { get; set; }

    /// <summary>Internal id of the zone that changed, if a zone changed.</summary>
    public int? ZoneId { get; set; }
}
