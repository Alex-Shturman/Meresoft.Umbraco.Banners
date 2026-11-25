using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>Stores zones. Methods use the ambient Umbraco scope.</summary>
internal interface IZoneRepository
{
    /// <summary>Inserts a zone and sets its <see cref="BannerZone.Id"/>.</summary>
    BannerZone Add(BannerZone zone);

    /// <summary>Updates all fields of a zone.</summary>
    void Update(BannerZone zone);

    /// <summary>Deletes a zone. Fails while an assignment refers to it.</summary>
    void Delete(int id);

    /// <summary>Finds a zone by its API key.</summary>
    BannerZone? GetByKey(Guid key);

    /// <summary>Finds a zone by its (normalised) alias.</summary>
    BannerZone? GetByAlias(string alias);

    /// <summary>Finds a zone by its internal id.</summary>
    BannerZone? GetById(int id);

    /// <summary>All zones ordered by name.</summary>
    IReadOnlyList<BannerZone> GetAll();

    /// <summary>Keys of the banners with a draft or live version assigned to the zone.</summary>
    IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId);
}
