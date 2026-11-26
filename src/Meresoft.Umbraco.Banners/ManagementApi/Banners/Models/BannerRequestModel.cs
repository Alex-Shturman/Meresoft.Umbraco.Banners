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

    /// <summary>
    /// Relative chance of being chosen, a whole number from 1 to 100. It is read as a number that may be missing or
    /// fractional, so a wrong value reaches the validator and comes back as an error on the weight field, not as a
    /// failed read of the whole body.
    /// </summary>
    public double? Weight { get; set; } = 1;

    /// <summary>First moment the banner may show, UTC.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>First moment the banner no longer shows, UTC.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>
    /// Impressions after which the banner stops, a whole number of 1 or more. Read as a number like the weight, so a
    /// fractional or too large value comes back as an error on this field, not as a failed read of the whole body.
    /// </summary>
    public double? ImpressionLimit { get; set; }

    /// <summary>Clicks after which the banner stops (image banners only), read like <see cref="ImpressionLimit"/>.</summary>
    public double? ClickLimit { get; set; }

    /// <summary>Keys of the zones the banner is assigned to.</summary>
    public IReadOnlyList<Guid> ZoneKeys { get; set; } = [];

    /// <summary>On save: the update date loaded by the client.</summary>
    public DateTime? UpdateDate { get; set; }

    // 0 is below the allowed range, so the validator reports it on the weight field.
    private static int WholeWeightOrZero(double? weight) =>
        weight is { } w && w >= 1 && w <= 100 && w == Math.Floor(w) ? (int)w : 0;

    // No limit stays no limit. A value that is not a whole number from 1 to 2^53 becomes 0, which the validator
    // reports on the limit's own field.
    private static long? WholeLimitOrZero(double? limit) =>
        limit is not { } l ? null : l >= 1 && l <= 9_007_199_254_740_992d && l == Math.Floor(l) ? (long)l : 0;

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
        Weight = WholeWeightOrZero(Weight),
        StartUtc = StartUtc,
        EndUtc = EndUtc,
        ImpressionLimit = WholeLimitOrZero(ImpressionLimit),
        ClickLimit = WholeLimitOrZero(ClickLimit),
        ZoneKeys = ZoneKeys,
        UpdateDate = UpdateDate,
    };
}
