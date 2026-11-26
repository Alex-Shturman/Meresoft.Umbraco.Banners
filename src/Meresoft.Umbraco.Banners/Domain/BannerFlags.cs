namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Runtime flags shown in the banner list.</summary>
[Flags]
public enum BannerFlags
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary>The version in use has no zone assignment.</summary>
    NotPlaced = 1,

    /// <summary>An image banner whose media item no longer exists.</summary>
    ImageMissing = 2,

    /// <summary>The start time has not been reached.</summary>
    Scheduled = 4,

    /// <summary>The end time has passed.</summary>
    Expired = 8,

    /// <summary>An impression or click limit is used up.</summary>
    LimitReached = 16,
}
