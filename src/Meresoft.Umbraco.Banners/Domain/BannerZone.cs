namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>A place on the site where banners are shown. Zones are global to the install.</summary>
public class BannerZone
{
    /// <summary>Internal id used in statistics and click addresses.</summary>
    public int Id { get; set; }

    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Alias used in templates.</summary>
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

    /// <summary>Creation time, UTC.</summary>
    public DateTime CreateDate { get; set; }

    /// <summary>Last change time, UTC.</summary>
    public DateTime UpdateDate { get; set; }
}
