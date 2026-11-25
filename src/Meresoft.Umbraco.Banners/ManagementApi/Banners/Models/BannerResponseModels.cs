using Meresoft.Umbraco.Banners.Domain;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;

/// <summary>Runtime flags of a banner.</summary>
public class BannerFlagsModel
{
    /// <summary>The version in use has no zone.</summary>
    public bool NotPlaced { get; set; }

    /// <summary>The image's media item no longer exists.</summary>
    public bool ImageMissing { get; set; }

    /// <summary>The start time has not been reached.</summary>
    public bool Scheduled { get; set; }

    /// <summary>The end time has passed.</summary>
    public bool Expired { get; set; }

    /// <summary>A limit is used up.</summary>
    public bool LimitReached { get; set; }

    internal static BannerFlagsModel From(BannerFlags flags) => new()
    {
        NotPlaced = flags.HasFlag(BannerFlags.NotPlaced),
        ImageMissing = flags.HasFlag(BannerFlags.ImageMissing),
        Scheduled = flags.HasFlag(BannerFlags.Scheduled),
        Expired = flags.HasFlag(BannerFlags.Expired),
        LimitReached = flags.HasFlag(BannerFlags.LimitReached),
    };
}

/// <summary>The content of one version (draft or live) of a banner.</summary>
public class BannerVersionModel
{
    /// <summary>Key of the Umbraco media item.</summary>
    public Guid? MediaKey { get; set; }

    /// <summary>The HTML (html banners).</summary>
    public string? Html { get; set; }

    /// <summary>Absolute http or https target address.</summary>
    public string? LinkUrl { get; set; }

    /// <summary>Open the link in a new window.</summary>
    public bool OpenInNewWindow { get; set; }

    /// <summary>Alternative text.</summary>
    public string AltText { get; set; } = string.Empty;

    /// <summary>Optional title.</summary>
    public string? Title { get; set; }

    /// <summary>Relative chance of being chosen.</summary>
    public int Weight { get; set; }

    /// <summary>First moment the banner may show, UTC.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>First moment the banner no longer shows, UTC.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Impressions after which the banner stops.</summary>
    public long? ImpressionLimit { get; set; }

    /// <summary>Clicks after which the banner stops.</summary>
    public long? ClickLimit { get; set; }

    /// <summary>Keys of the assigned zones.</summary>
    public IReadOnlyList<Guid> ZoneKeys { get; set; } = [];

    /// <summary>Time of the last save, UTC.</summary>
    public DateTime SavedDate { get; set; }

    internal static BannerVersionModel? From(BannerVersion? version, IReadOnlyDictionary<int, Guid> zoneKeys) => version is null
        ? null
        : new BannerVersionModel
        {
            MediaKey = version.MediaKey,
            Html = version.Html,
            LinkUrl = version.LinkUrl,
            OpenInNewWindow = version.OpenInNewWindow,
            AltText = version.AltText,
            Title = version.Title,
            Weight = version.Weight,
            StartUtc = version.StartUtc,
            EndUtc = version.EndUtc,
            ImpressionLimit = version.ImpressionLimit,
            ClickLimit = version.ClickLimit,
            ZoneKeys = version.ZoneIds.Where(zoneKeys.ContainsKey).Select(id => zoneKeys[id]).ToList(),
            SavedDate = version.SavedDate,
        };
}

/// <summary>A banner with its draft and live versions.</summary>
public class BannerResponseModel
{
    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image or html.</summary>
    public BannerType Type { get; set; }

    /// <summary>Publish state.</summary>
    public BannerState State { get; set; }

    /// <summary>Runtime flags.</summary>
    public BannerFlagsModel Flags { get; set; } = new();

    /// <summary>The draft version.</summary>
    public BannerVersionModel? Draft { get; set; }

    /// <summary>The live version; null if never published.</summary>
    public BannerVersionModel? Live { get; set; }

    /// <summary>Creation time, UTC.</summary>
    public DateTime CreateDate { get; set; }

    /// <summary>Last change time, UTC; send it back on save and publish.</summary>
    public DateTime UpdateDate { get; set; }

    /// <summary>Time of the last publish, UTC.</summary>
    public DateTime? PublishDate { get; set; }

    internal static BannerResponseModel From(Banner banner, IReadOnlyDictionary<int, Guid> zoneKeys) => new()
    {
        Key = banner.Key,
        Name = banner.Name,
        Type = banner.Type,
        State = banner.State,
        Flags = BannerFlagsModel.From(banner.Flags),
        Draft = BannerVersionModel.From(banner.Draft, zoneKeys),
        Live = BannerVersionModel.From(banner.Live, zoneKeys),
        CreateDate = banner.CreateDate,
        UpdateDate = banner.UpdateDate,
        PublishDate = banner.PublishDate,
    };
}

/// <summary>One row of the banner list.</summary>
public class BannerListItemModel
{
    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image or html.</summary>
    public BannerType Type { get; set; }

    /// <summary>Publish state.</summary>
    public BannerState State { get; set; }

    /// <summary>Runtime flags.</summary>
    public BannerFlagsModel Flags { get; set; } = new();

    /// <summary>Keys of the zones of the version in use.</summary>
    public IReadOnlyList<Guid> ZoneKeys { get; set; } = [];

    /// <summary>Weight of the version in use.</summary>
    public int Weight { get; set; }

    /// <summary>Last change time, UTC.</summary>
    public DateTime UpdateDate { get; set; }
}

/// <summary>A page of the banner list.</summary>
public class BannerPageModel
{
    /// <summary>All matching banners, not only this page.</summary>
    public long Total { get; set; }

    /// <summary>The banners of this page.</summary>
    public IReadOnlyList<BannerListItemModel> Items { get; set; } = [];
}
