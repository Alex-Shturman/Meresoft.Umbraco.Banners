using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>Data to create a banner: its fixed type and its first draft.</summary>
public class BannerCreateModel
{
    /// <summary>Image or html; fixed after creation.</summary>
    public BannerType Type { get; set; }

    /// <summary>The first draft, including the name.</summary>
    public BannerDraftModel Draft { get; set; } = new();
}
