using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Saves through the guarded repository methods, using the update date that is stored right now as the expected one,
/// as a test that sets up data needs. A missing row or a lost race throws, as the removed unguarded methods did.
/// </summary>
internal static class RepositoryTestExtensions
{
    public static void SaveDraftAtStoredDate(this IBannerRepository banners, Banner banner)
    {
        if (!banners.SaveDraft(banner, StoredUpdateDate(banners, banner.Id)))
        {
            throw new InvalidOperationException($"Banner {banner.Id} does not exist.");
        }
    }

    public static void CopyDraftOverLiveAtStoredDate(this IBannerRepository banners, int bannerId, DateTime publishDateUtc)
    {
        if (!banners.CopyDraftOverLive(bannerId, publishDateUtc, StoredUpdateDate(banners, bannerId)))
        {
            throw new InvalidOperationException($"Banner {bannerId} does not exist.");
        }
    }

    public static void UpdateAtStoredDate(this IZoneRepository zones, BannerZone zone)
    {
        DateTime stored = zones.GetById(zone.Id)?.UpdateDate ?? throw new InvalidOperationException($"Zone {zone.Id} does not exist.");
        if (!zones.Update(zone, stored))
        {
            throw new InvalidOperationException($"Zone {zone.Id} does not exist.");
        }
    }

    private static DateTime StoredUpdateDate(IBannerRepository banners, int bannerId) =>
        banners.GetById(bannerId)?.UpdateDate ?? throw new InvalidOperationException($"Banner {bannerId} does not exist.");
}
