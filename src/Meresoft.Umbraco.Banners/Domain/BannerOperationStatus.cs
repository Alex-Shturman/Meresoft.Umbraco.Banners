namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Result of a banner or zone operation of the public services.</summary>
public enum BannerOperationStatus
{
    /// <summary>The operation succeeded.</summary>
    Success = 0,

    /// <summary>The banner or zone does not exist.</summary>
    NotFound,

    /// <summary>The data failed validation.</summary>
    InvalidData,

    /// <summary>Another zone already has this alias.</summary>
    DuplicateAlias,

    /// <summary>The zone still has banners assigned.</summary>
    ZoneInUse,

    /// <summary>The item was changed by someone else after it was loaded.</summary>
    ChangedByAnotherUser,

    /// <summary>The banner has no zone assignment, so it cannot be published.</summary>
    NoZoneAssigned,

    /// <summary>The type of a banner cannot be changed after creation.</summary>
    TypeChangeNotAllowed,
}
