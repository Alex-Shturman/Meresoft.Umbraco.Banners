using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;

/// <summary>A zone as the management API returns it.</summary>
public class ZoneResponseModel
{
    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Alias used in templates.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>How many banners are shown at once.</summary>
    public int ShowCount { get; set; }

    /// <summary>Image width in pixels.</summary>
    public int? ImageWidth { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int? ImageHeight { get; set; }

    /// <summary>Server or client rendering.</summary>
    public ZoneRenderMode RenderMode { get; set; }

    /// <summary>Last change time, UTC; send it back on update.</summary>
    public DateTime UpdateDate { get; set; }

    /// <summary>Banners that are shown in the zone right now (list only).</summary>
    public int ActiveBannerCount { get; set; }

    internal static ZoneResponseModel From(BannerZone zone, int activeBannerCount = 0) => new()
    {
        Key = zone.Key,
        Alias = zone.Alias,
        Name = zone.Name,
        Description = zone.Description,
        ShowCount = zone.ShowCount,
        ImageWidth = zone.ImageWidth,
        ImageHeight = zone.ImageHeight,
        RenderMode = zone.RenderMode,
        UpdateDate = zone.UpdateDate,
        ActiveBannerCount = activeBannerCount,
    };
}
