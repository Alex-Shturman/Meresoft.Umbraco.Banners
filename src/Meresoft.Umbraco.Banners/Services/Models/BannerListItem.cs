using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>One row of the banner list.</summary>
public class BannerListItem
{
    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Name, shown in the back office only.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image or html.</summary>
    public BannerType Type { get; set; }

    /// <summary>Publish state.</summary>
    public BannerState State { get; set; }

    /// <summary>Runtime flags.</summary>
    public BannerFlags Flags { get; set; }

    /// <summary>Keys of the zones of the version in use (live, or draft when never published).</summary>
    public IReadOnlyList<Guid> ZoneKeys { get; set; } = [];

    /// <summary>Weight of the version in use.</summary>
    public int Weight { get; set; }

    /// <summary>Last change time, UTC.</summary>
    public DateTime UpdateDate { get; set; }
}
