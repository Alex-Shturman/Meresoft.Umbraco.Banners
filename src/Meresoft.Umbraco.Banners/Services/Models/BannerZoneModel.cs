using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>The editable fields of a zone, used to create and update zones.</summary>
public class BannerZoneModel
{
    /// <summary>Alias used in templates; trimmed and lowercased when stored.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>How many banners are shown at once, 1 to 10.</summary>
    public int ShowCount { get; set; } = 1;

    /// <summary>Image width in pixels, if the zone fixes it.</summary>
    public int? ImageWidth { get; set; }

    /// <summary>Image height in pixels, if the zone fixes it.</summary>
    public int? ImageHeight { get; set; }

    /// <summary>Server or client rendering.</summary>
    public ZoneRenderMode RenderMode { get; set; } = ZoneRenderMode.Server;

    /// <summary>On update: the <see cref="BannerZone.UpdateDate"/> the caller loaded. A different stored value gives ChangedByAnotherUser; null skips the check.</summary>
    public DateTime? UpdateDate { get; set; }
}
