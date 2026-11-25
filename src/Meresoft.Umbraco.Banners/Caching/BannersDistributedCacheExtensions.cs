using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Scoping;

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

    /// <summary>
    /// Sends the refresh message only when the scope, or the outermost scope it joined, has committed. A message
    /// sent earlier lets a server rebuild from uncommitted data, and survives a rollback.
    /// </summary>
    internal static void RefreshBannersAfterCommit(this DistributedCache distributedCache, ICoreScopeProvider scopeProvider, BannersCacheRefresherPayload payload)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);
        void Send(bool completed)
        {
            if (completed)
            {
                distributedCache.RefreshBanners(payload);
            }
        }

        if (scopeProvider.Context is { } context)
        {
            context.Enlist(Guid.NewGuid().ToString(), () => 0, (completed, _) => Send(completed));
        }
        else
        {
            // Without a scope context the work is not transactional with the caller; the caller completes the scope next.
            Send(true);
        }
    }
}
