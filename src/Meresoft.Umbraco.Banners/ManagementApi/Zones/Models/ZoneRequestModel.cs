using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;

/// <summary>Body of the zone create and update requests.</summary>
public class ZoneRequestModel
{
    /// <summary>Alias used in templates.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>How many banners are shown at once, 1 to 10.</summary>
    public int ShowCount { get; set; } = 1;

    /// <summary>Image width in pixels.</summary>
    public int? ImageWidth { get; set; }

    /// <summary>Image height in pixels.</summary>
    public int? ImageHeight { get; set; }

    /// <summary>Server or client rendering.</summary>
    public ZoneRenderMode RenderMode { get; set; } = ZoneRenderMode.Server;

    /// <summary>On update: the update date loaded by the client.</summary>
    public DateTime? UpdateDate { get; set; }

    internal BannerZoneModel ToServiceModel() => new()
    {
        Alias = Alias,
        Name = Name,
        Description = Description,
        ShowCount = ShowCount,
        ImageWidth = ImageWidth,
        ImageHeight = ImageHeight,
        RenderMode = RenderMode,
        UpdateDate = UpdateDate,
    };
}
