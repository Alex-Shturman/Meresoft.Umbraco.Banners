using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>The editable content of a banner draft, used to create a banner and to save its draft.</summary>
public class BannerDraftModel
{
    /// <summary>Name, shown in the back office only.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>On save: the banner type the caller expects; a different type gives TypeChangeNotAllowed. Null keeps the type.</summary>
    public BannerType? Type { get; set; }

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

    /// <summary>Keys of the zones the banner is assigned to.</summary>
    public IReadOnlyList<Guid> ZoneKeys { get; set; } = [];

    /// <summary>On save: the <see cref="Banner.UpdateDate"/> the caller loaded. A different stored value gives ChangedByAnotherUser; null skips the check.</summary>
    public DateTime? UpdateDate { get; set; }
}
