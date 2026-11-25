namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>The editable content of a banner: either its draft or its live version.</summary>
public class BannerVersion
{
    /// <summary>Row id.</summary>
    public int Id { get; set; }

    /// <summary>Id of the banner.</summary>
    public int BannerId { get; set; }

    /// <summary>Draft or live.</summary>
    public BannerVersionKind Kind { get; set; } = BannerVersionKind.Draft;

    /// <summary>Key of the Umbraco media item (image banners).</summary>
    public Guid? MediaKey { get; set; }

    /// <summary>The HTML (html banners).</summary>
    public string? Html { get; set; }

    /// <summary>Absolute http or https target address.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Open the link in a new window.</summary>
    public bool OpenInNewWindow { get; set; }

    /// <summary>Alternative text of the image.</summary>
    public string AltText { get; set; } = string.Empty;

    /// <summary>Optional title.</summary>
    public string? Title { get; set; }

    /// <summary>Relative chance of being chosen, 1 to 100.</summary>
    public int Weight { get; set; } = 1;

    /// <summary>First moment the banner may show, UTC.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>First moment the banner no longer shows, UTC.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Impressions after which the banner stops.</summary>
    public long? ImpressionLimit { get; set; }

    /// <summary>Clicks after which the banner stops (image banners only).</summary>
    public long? ClickLimit { get; set; }

    /// <summary>Time of the last save, UTC.</summary>
    public DateTime SavedDate { get; set; }

    /// <summary>Key of the back office user who saved.</summary>
    public Guid SavedBy { get; set; }

    /// <summary>Ids of the zones this version is assigned to.</summary>
    public List<int> ZoneIds { get; set; } = [];
}
