using Meresoft.Umbraco.Banners.Domain;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;

namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>
/// Builds a <see cref="LiveSnapshot"/> from zones and published banners. Media items are resolved
/// here, once, so a page request never has to look them up (SC-003).
/// </summary>
internal sealed class LiveSnapshotBuilder
{
    private const string MediaFileProperty = "umbracoFile";

    // The relative mode never reads the base address; the providers only need some address to be given.
    private static readonly Uri NoRequestBase = new("http://localhost/");

    private readonly IPublishedMediaCache _mediaCache;
    private readonly MediaUrlProviderCollection _mediaUrlProviders;
    private readonly IImageUrlGenerator _imageUrlGenerator;

    public LiveSnapshotBuilder(IPublishedMediaCache mediaCache, MediaUrlProviderCollection mediaUrlProviders, IImageUrlGenerator imageUrlGenerator)
    {
        _mediaCache = mediaCache ?? throw new ArgumentNullException(nameof(mediaCache));
        _mediaUrlProviders = mediaUrlProviders ?? throw new ArgumentNullException(nameof(mediaUrlProviders));
        _imageUrlGenerator = imageUrlGenerator ?? throw new ArgumentNullException(nameof(imageUrlGenerator));
    }

    /// <summary>Builds the snapshot. Banners that are not published, have no live version or no zone are skipped.</summary>
    public async Task<LiveSnapshot> BuildAsync(
        IReadOnlyList<BannerZone> zones,
        IReadOnlyList<Banner> banners,
        IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(banners);

        // Html banners join the snapshot with their own task (US6); only image banners are candidates here.
        Banner[] shown = banners
            .Where(b => b is { IsPublished: true, Live: not null, Type: BannerType.Image })
            .ToArray();

        var missing = new HashSet<int>();
        var baseUrls = new Dictionary<Guid, string?>();
        foreach (Banner banner in shown)
        {
            Guid mediaKey = banner.Live!.MediaKey ?? Guid.Empty;
            if (!baseUrls.TryGetValue(mediaKey, out string? url))
            {
                url = mediaKeysTreatedAsMissing?.Contains(mediaKey) == true ? null : await ResolveMediaUrlAsync(mediaKey);
                baseUrls[mediaKey] = url;
            }

            if (url is null)
            {
                missing.Add(banner.Id);
            }
        }

        var result = new Dictionary<string, SnapshotZone>(StringComparer.Ordinal);
        foreach (BannerZone zone in zones)
        {
            var candidates = new List<SnapshotBanner>();
            foreach (Banner banner in shown.Where(b => b.Live!.ZoneIds.Contains(zone.Id) && !missing.Contains(b.Id)))
            {
                BannerVersion live = banner.Live!;
                string imageUrl = baseUrls[live.MediaKey ?? Guid.Empty]!;
                if (zone.ImageWidth is not null || zone.ImageHeight is not null)
                {
                    imageUrl = _imageUrlGenerator.GetImageUrl(new ImageUrlGenerationOptions(imageUrl)
                    {
                        Width = zone.ImageWidth,
                        Height = zone.ImageHeight,
                    }) ?? imageUrl;
                }

                candidates.Add(new SnapshotBanner
                {
                    BannerId = banner.Id,
                    ZoneId = zone.Id,
                    ImageUrl = imageUrl,
                    Width = zone.ImageWidth,
                    Height = zone.ImageHeight,
                    AltText = live.AltText,
                    Title = live.Title,
                    LinkUrl = live.LinkUrl ?? string.Empty,
                    OpenInNewWindow = live.OpenInNewWindow,
                    Weight = live.Weight,
                    StartUtc = live.StartUtc,
                    EndUtc = live.EndUtc,
                    ImpressionLimit = live.ImpressionLimit,
                    ClickLimit = live.ClickLimit,
                });
            }

            string alias = AliasNormalizer.Normalize(zone.Alias);
            result[alias] = new SnapshotZone(zone.Id, alias, zone.ShowCount, zone.RenderMode, zone.ImageWidth, zone.ImageHeight, candidates);
        }

        return new LiveSnapshot(result, missing, baseUrls.Keys.ToHashSet());
    }

    private async Task<string?> ResolveMediaUrlAsync(Guid mediaKey)
    {
        if (mediaKey == Guid.Empty)
        {
            return null;
        }

        IPublishedContent? media = await _mediaCache.GetByIdAsync(mediaKey);
        if (media is null)
        {
            return null;
        }

        // The registered media URL providers are asked one by one, directly and in relative mode, so this works in
        // background work (start up, safety reload) where no request exists, and a custom provider (CDN, path base)
        // is honoured. The aggregate UrlProvider is not used: it reads the request URL, which does not exist there.
        // If no provider answers, the stored file path of the property is used as it is.
        string? url = null;
        foreach (IMediaUrlProvider provider in _mediaUrlProviders)
        {
            UrlInfo? info = provider.GetMediaUrl(media, MediaFileProperty, UrlMode.Relative, null, NoRequestBase);
            if (info?.Url is { } address)
            {
                url = !address.IsAbsoluteUri
                    ? address.OriginalString
                    : address.Authority == NoRequestBase.Authority
                        ? address.PathAndQuery // Built from the placeholder base, so it is a site relative address.
                        : address.AbsoluteUri;
                break;
            }
        }

        url ??= media.GetProperty(MediaFileProperty)?.GetValue() switch
        {
            ImageCropperValue cropper => cropper.Src,
            string path => path,
            _ => null,
        };
        return string.IsNullOrEmpty(url) ? null : url;
    }
}
