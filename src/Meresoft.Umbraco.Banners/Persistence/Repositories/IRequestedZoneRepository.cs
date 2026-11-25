using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>Stores the zone aliases that templates asked for. Methods use the ambient Umbraco scope.</summary>
internal interface IRequestedZoneRepository
{
    /// <summary>Inserts the alias, or keeps its first seen time and moves its last seen time forward.</summary>
    void Upsert(string alias, DateTime firstSeenUtc, DateTime lastSeenUtc);

    /// <summary>Removes the alias; false when it was not stored.</summary>
    bool Delete(string alias);

    /// <summary>All stored aliases, oldest first seen first.</summary>
    IReadOnlyList<RequestedZone> GetAll();
}
