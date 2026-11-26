using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// The in-memory view of the live banners, read by every page request. It is immutable: a new one
/// is built on publish and unpublish and swapped in atomically (research R8).
/// </summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public sealed class LiveSnapshot
{
    /// <summary>A snapshot with no zones.</summary>
    public static readonly LiveSnapshot Empty = new(new Dictionary<string, SnapshotZone>(), new HashSet<int>(), new HashSet<Guid>());

    /// <summary>Creates a snapshot.</summary>
    public LiveSnapshot(IReadOnlyDictionary<string, SnapshotZone> zones, IReadOnlySet<int> imageMissingBannerIds, IReadOnlySet<Guid> mediaKeys)
    {
        Zones = zones;
        foreach (SnapshotZone zone in zones.Values)
        {
            foreach (SnapshotBanner candidate in zone.Candidates)
            {
                _ = candidate.Selected; // Built here, once, so no page request builds it.
            }
        }

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
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public sealed record SnapshotZone(
    int Id,
    string Alias,
    int ShowCount,
    ZoneRenderMode RenderMode,
    int? ImageWidth,
    int? ImageHeight,
    IReadOnlyList<SnapshotBanner> Candidates);

/// <summary>A banner that can be shown in a zone, with everything the page needs pre-computed.</summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
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

    /// <summary>Stored markup of an Html banner; null for an image banner (the image members are then empty).</summary>
    public string? Html { get; init; }

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

    // Holds the SelectedBanner. Record equality compares every field, so the holder says all holders are equal: the
    // cached value is derived purely from the other properties and must not make two equal banners unequal.
    private SelectedHolder _holder = new();

    /// <summary>Copy constructor of <c>with</c>: the copy starts with an empty holder, so it never shows the original's stale value.</summary>
    // ADD EVERY NEW PROPERTY HERE. This constructor is written by hand, so a property added to the record and not copied
    // below is silently lost by `with`. SnapshotBannerCopyTests fails when that happens.
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    private SnapshotBanner(SnapshotBanner original)
    {
        BannerId = original.BannerId;
        ZoneId = original.ZoneId;
        ImageUrl = original.ImageUrl;
        Width = original.Width;
        Height = original.Height;
        AltText = original.AltText;
        Title = original.Title;
        LinkUrl = original.LinkUrl;
        OpenInNewWindow = original.OpenInNewWindow;
        Html = original.Html;
        Weight = original.Weight;
        StartUtc = original.StartUtc;
        EndUtc = original.EndUtc;
        ImpressionLimit = original.ImpressionLimit;
        ClickLimit = original.ClickLimit;
        _holder = new SelectedHolder();
    }

    /// <summary>
    /// What the markup needs, derived purely from the other properties. <see cref="LiveSnapshot"/> builds it for every
    /// candidate when the snapshot is created, so a page request allocates nothing for it; it is built on first use
    /// for a banner that is read outside a snapshot. Two threads may build it at once; both results are equal and
    /// immutable, so either may win.
    /// </summary>
    internal SelectedBanner Selected => _holder.Value ??= new SelectedBanner
    {
        BannerId = BannerId,
        ZoneId = ZoneId,
        ImageUrl = ImageUrl,
        Width = Width,
        Height = Height,
        AltText = AltText,
        Title = Title,
        LinkUrl = LinkUrl,
        OpenInNewWindow = OpenInNewWindow,
        Html = Html,
    };

    /// <summary>True once <see cref="Selected"/> has been built.</summary>
    internal bool IsSelectedBuilt => _holder.Value is not null;

    private sealed class SelectedHolder
    {
        public SelectedBanner? Value;

        public override bool Equals(object? obj) => obj is SelectedHolder;

        public override int GetHashCode() => 0;
    }
}
