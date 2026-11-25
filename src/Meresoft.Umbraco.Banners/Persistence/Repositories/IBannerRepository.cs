using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>Stores banners with their draft and live versions. Methods use the ambient Umbraco scope.</summary>
internal interface IBannerRepository
{
    /// <summary>Inserts a banner with its draft (and live, when set) version and assignments; sets all ids.</summary>
    Banner Add(Banner banner);

    /// <summary>Finds a banner with both versions and their zone ids by API key.</summary>
    Banner? GetByKey(Guid key);

    /// <summary>Finds a banner with both versions and their zone ids by internal id.</summary>
    Banner? GetById(int id);

    /// <summary>Updates the banner row (name, update fields) and replaces its draft and the draft assignments. Never touches the live version.</summary>
    void SaveDraft(Banner banner);

    /// <summary>Copies the draft (fields and assignments) over the live version, creating it when missing, and marks the banner published.</summary>
    void CopyDraftOverLive(int bannerId, DateTime publishDateUtc);

    /// <summary>Switches the banner on or off; the live version stays.</summary>
    void SetPublished(int bannerId, bool isPublished);

    /// <summary>Deletes the banner, its versions and assignments. Statistic rows stay.</summary>
    void Delete(int id);

    /// <summary>All published banners (versions and zone ids included), for building the live snapshot.</summary>
    IReadOnlyList<Banner> GetAllPublished();

    /// <summary>A page of banners (versions included) and the total count.</summary>
    (IReadOnlyList<Banner> Items, long Total) GetPaged(BannerFilter filter);
}
