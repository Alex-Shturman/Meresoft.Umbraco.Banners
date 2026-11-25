namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Which of the two versions of a banner a row is.</summary>
public enum BannerVersionKind : byte
{
    /// <summary>The version editors work on.</summary>
    Draft = 0,

    /// <summary>The version shown on the site.</summary>
    Live = 1,
}
