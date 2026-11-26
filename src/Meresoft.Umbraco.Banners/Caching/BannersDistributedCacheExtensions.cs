using Umbraco.Cms.Core.Cache;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Sends banner refresh messages through Umbraco's distributed cache.</summary>
public static class BannersDistributedCacheExtensions
{
    /// <summary>Tells all servers that banners or zones changed.</summary>
    public static void RefreshBanners(this DistributedCache distributedCache, params BannersCacheRefresherPayload[] payloads)
    {
        ArgumentNullException.ThrowIfNull(distributedCache);
        distributedCache.RefreshByPayload(BannersCacheRefresher.UniqueId, payloads);
    }
}
