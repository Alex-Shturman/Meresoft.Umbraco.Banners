using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.Services;

/// <summary>Creates, changes and deletes banner zones. Every call runs in its own Umbraco scope.</summary>
public interface IBannerZoneService
{
    /// <summary>All zones ordered by name.</summary>
    Task<IReadOnlyList<BannerZone>> GetAllAsync();

    /// <summary>A zone by its management API key.</summary>
    Task<BannerZone?> GetAsync(Guid key);

    /// <summary>A zone by its alias; the alias is normalised first.</summary>
    Task<BannerZone?> GetByAliasAsync(string alias);

    /// <summary>Creates a zone and removes the matching requested zone.</summary>
    Task<Attempt<BannerZone, BannerOperationStatus>> CreateAsync(BannerZoneModel model, Guid userKey);

    /// <summary>Changes a zone; <see cref="BannerZoneModel.UpdateDate"/> guards against a concurrent change.</summary>
    Task<Attempt<BannerZone, BannerOperationStatus>> UpdateAsync(Guid key, BannerZoneModel model, Guid userKey);

    /// <summary>Deletes a zone; refused with ZoneInUse while any banner version is assigned to it.</summary>
    Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey);

    /// <summary>Zone aliases that templates asked for but that do not exist yet.</summary>
    Task<IReadOnlyList<RequestedZone>> GetRequestedZonesAsync();

    /// <summary>Removes an alias from the requested zones; it comes back when a template asks for it again.</summary>
    Task DismissRequestedZoneAsync(string alias);

    /// <summary>Keys of the banners with a draft or live version assigned to the zone.</summary>
    Task<IReadOnlyList<Guid>> GetAssignedBannerKeysAsync(Guid zoneKey);
}
