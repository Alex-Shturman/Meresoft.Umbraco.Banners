namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Kind of banner; fixed after creation.</summary>
public enum BannerType : byte
{
    /// <summary>A media item with a link.</summary>
    Image = 0,

    /// <summary>Stored HTML shown as is.</summary>
    Html = 1,
}
