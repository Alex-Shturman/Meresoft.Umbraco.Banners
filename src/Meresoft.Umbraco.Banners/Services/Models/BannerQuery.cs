using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>Filters and paging of the banner list.</summary>
public class BannerQuery
{
    /// <summary>Largest page size.</summary>
    public const int MaxTake = 100;

    /// <summary>Only banners assigned to this zone.</summary>
    public Guid? ZoneKey { get; set; }

    /// <summary>Only banners in this publish state.</summary>
    public BannerState? State { get; set; }

    /// <summary>Only banners of this type.</summary>
    public BannerType? Type { get; set; }

    /// <summary>Rows to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Rows to take, 1 to <see cref="MaxTake"/>.</summary>
    public int Take { get; set; } = 50;
}
