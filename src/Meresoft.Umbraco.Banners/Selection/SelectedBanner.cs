namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>A banner chosen for one view of a zone, with what the markup needs.</summary>
public sealed record SelectedBanner
{
    /// <summary>Internal banner id.</summary>
    public required int BannerId { get; init; }

    /// <summary>Internal id of the zone.</summary>
    public required int ZoneId { get; init; }

    /// <summary>Image address.</summary>
    public required string ImageUrl { get; init; }

    /// <summary>Image width, if the zone fixes it.</summary>
    public int? Width { get; init; }

    /// <summary>Image height, if the zone fixes it.</summary>
    public int? Height { get; init; }

    /// <summary>Alternative text.</summary>
    public required string AltText { get; init; }

    /// <summary>Optional title.</summary>
    public string? Title { get; init; }

    /// <summary>Target address of the link.</summary>
    public required string LinkUrl { get; init; }

    /// <summary>Open the link in a new window.</summary>
    public bool OpenInNewWindow { get; init; }
}
