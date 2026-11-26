namespace Meresoft.Umbraco.Banners;

/// <summary>Fixed names used across the package: part of the stable public surface.</summary>
public static class BannersConstants
{
    /// <summary>Name of the package migration plan.</summary>
    public const string MigrationPlanName = "Meresoft.Umbraco.Banners";

    /// <summary>Alias of the back office section.</summary>
    public const string SectionAlias = "Meresoft.Banners.Section";

    /// <summary>Permission verb that allows changing HTML banners.</summary>
    public const string HtmlPermission = "Meresoft.Banners.Html";

    /// <summary>Authorization policy that requires access to the Banners section.</summary>
    public const string SectionAccessPolicy = "Meresoft.Banners.SectionAccess";

    /// <summary>Name of the management API and its OpenAPI document.</summary>
    public const string ApiName = "meresoft-banners";

    /// <summary>API explorer group name of the management API controllers.</summary>
    public const string ApiGroupName = "Banners";

    /// <summary>
    /// Id of the package's own row in the Umbraco lock table (<c>umbracoLock</c>), added by the package migration. Every scope
    /// of the package that writes takes this write lock first, so writers wait in line before they read instead of
    /// upgrading a read to a write and stalling each other (SQLite shared cache).
    /// </summary>
    public const int WriteLockId = -8_201_001;

    /// <summary>The most rows one list or history request returns; the server clamps or rejects a larger page.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Audit entity type of a banner.</summary>
    public const string BannerEntityType = "meresoft-banner";

    /// <summary>Audit entity type of a zone.</summary>
    public const string ZoneEntityType = "meresoft-banner-zone";
}
