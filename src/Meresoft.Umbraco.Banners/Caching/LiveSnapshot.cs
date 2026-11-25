using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// The in-memory view of the live banners, read by every page request. It is immutable: a new one
/// is built on publish and unpublish and swapped in atomically (research R8).
/// </summary>
public sealed class LiveSnapshot
{
    /// <summary>A snapshot with no zones.</summary>
    public static readonly LiveSnapshot Empty = new(new Dictionary<string, SnapshotZone>(), new HashSet<int>(), new HashSet<Guid>());

    /// <summary>Creates a snapshot.</summary>
    public LiveSnapshot(IReadOnlyDictionary<string, SnapshotZone> zones, IReadOnlySet<int> imageMissingBannerIds, IReadOnlySet<Guid> mediaKeys)
    {
        Zones = zones;
        ImageMissingBannerIds = imageMissingBannerIds;
        MediaKeys = mediaKeys;
    }

    /// <summary>Zones by normalised alias.</summary>
    public IReadOnlyDictionary<string, SnapshotZone> Zones { get; }

    /// <summary>Keys of the media items used by published banners, including missing ones.</summary>
    public IReadOnlySet<Guid> MediaKeys { get; }

    /// <summary>Ids of published image banners whose media item no longer exists.</summary>
    public IReadOnlySet<int> ImageMissingBannerIds { get; }
}

/// <summary>A zone with the banners that can be shown in it.</summary>
/// <param name="Id">Internal zone id.</param>
/// <param name="Alias">Normalised alias.</param>
/// <param name="ShowCount">How many banners are shown at once.</param>
/// <param name="RenderMode">Server or client rendering.</param>
/// <param name="ImageWidth">Fixed image width, if any.</param>
/// <param name="ImageHeight">Fixed image height, if any.</param>
/// <param name="Candidates">Published banners assigned to the zone.</param>
public sealed record SnapshotZone(
    int Id,
    string Alias,
    int ShowCount,
    ZoneRenderMode RenderMode,
    int? ImageWidth,
    int? ImageHeight,
    IReadOnlyList<SnapshotBanner> Candidates);

/// <summary>A banner that can be shown in a zone, with everything the page needs pre-computed.</summary>
public sealed record SnapshotBanner
{
    /// <summary>Internal banner id.</summary>
    public required int BannerId { get; init; }

    /// <summary>Internal id of the zone this candidate belongs to.</summary>
    public required int ZoneId { get; init; }

    /// <summary>The image address, built once at snapshot build time.</summary>
    public required string ImageUrl { get; init; }

    /// <summary>Image width, from the zone.</summary>
    public int? Width { get; init; }

    /// <summary>Image height, from the zone.</summary>
    public int? Height { get; init; }

    /// <summary>Alternative text.</summary>
    public required string AltText { get; init; }

    /// <summary>Optional title.</summary>
    public string? Title { get; init; }

    /// <summary>Stored target address.</summary>
    public required string LinkUrl { get; init; }

    /// <summary>Open the link in a new window.</summary>
    public bool OpenInNewWindow { get; init; }

    /// <summary>Relative chance of being chosen.</summary>
    public int Weight { get; init; } = 1;

    /// <summary>First moment the banner may show, UTC.</summary>
    public DateTime? StartUtc { get; init; }

    /// <summary>First moment the banner no longer shows, UTC.</summary>
    public DateTime? EndUtc { get; init; }

    /// <summary>Impressions after which the banner stops.</summary>
    public long? ImpressionLimit { get; init; }

    /// <summary>Clicks after which the banner stops.</summary>
    public long? ClickLimit { get; init; }
}
