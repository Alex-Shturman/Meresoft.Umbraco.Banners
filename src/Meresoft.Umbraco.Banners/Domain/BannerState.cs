namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Publish state of a banner (data-model.md).</summary>
public enum BannerState
{
    /// <summary>Never published.</summary>
    Draft = 0,

    /// <summary>Live and the draft equals the live version.</summary>
    Published = 1,

    /// <summary>Live, with draft changes saved after the last publish.</summary>
    PublishedWithChanges = 2,

    /// <summary>Was published, now switched off.</summary>
    Unpublished = 3,
}
