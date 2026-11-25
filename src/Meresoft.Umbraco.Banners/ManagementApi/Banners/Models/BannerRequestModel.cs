using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;

/// <summary>Body of the banner create and save requests: the name and the draft fields.</summary>
public class BannerRequestModel
{
    /// <summary>Name, shown in the back office only.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image or html. On create it sets the type; on save it must equal the stored type.</summary>
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

    /// <summary>On save: the update date loaded by the client.</summary>
    public DateTime? UpdateDate { get; set; }

    internal BannerDraftModel ToDraftModel() => new()
    {
        Name = Name,
        Type = Type,
        MediaKey = MediaKey,
        Html = Html,
        LinkUrl = LinkUrl,
        OpenInNewWindow = OpenInNewWindow,
        AltText = AltText,
        Title = Title,
        Weight = Weight,
        StartUtc = StartUtc,
        EndUtc = EndUtc,
        ImpressionLimit = ImpressionLimit,
        ClickLimit = ClickLimit,
        ZoneKeys = ZoneKeys,
        UpdateDate = UpdateDate,
    };
}
