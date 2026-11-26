namespace Meresoft.Umbraco.Banners.Services.Audit;

/// <summary>The changes of a banner or zone that are written to the audit history.</summary>
public enum BannerAuditAction
{
    /// <summary>Created.</summary>
    New,

    /// <summary>Saved.</summary>
    Save,

    /// <summary>Published.</summary>
    Publish,

    /// <summary>Unpublished.</summary>
    Unpublish,

    /// <summary>Copied.</summary>
    Copy,

    /// <summary>Deleted.</summary>
    Delete,
}
