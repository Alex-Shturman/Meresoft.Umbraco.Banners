using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>Filters and paging of the banner list.</summary>
internal sealed class BannerFilter
{
    /// <summary>Only banners whose version in use is assigned to this zone (internal id).</summary>
    public int? ZoneId { get; set; }

    /// <summary>Only banners of this type.</summary>
    public BannerType? Type { get; set; }

    /// <summary>Only banners in this publish state.</summary>
    public BannerState? State { get; set; }

    /// <summary>Rows to skip.</summary>
    public int Skip { get; set; }

    /// <summary>Rows to take, at most 100.</summary>
    public int Take { get; set; } = 50;
}
