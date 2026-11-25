using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>Chooses and writes a zone for the tag helper and the view component. Reads memory only.</summary>
public sealed class BannerZoneRenderer
{
    private readonly IBannerSelector _selector;
    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly RequestedZoneRecorder _recorder;
    private readonly BannerZoneHtmlWriter _writer;

    /// <summary>Creates the renderer.</summary>
    public BannerZoneRenderer(IBannerSelector selector, ILiveSnapshotAccessor snapshot, RequestedZoneRecorder recorder, BannerZoneHtmlWriter writer)
    {
        _selector = selector;
        _snapshot = snapshot;
        _recorder = recorder;
        _writer = writer;
    }

    /// <summary>Renders a zone; empty when there is nothing to show and no fallback.</summary>
    public string Render(IDictionary<object, object?> requestItems, string? alias, string? cssClass, string? fallbackHtml)
    {
        string normalised = AliasNormalizer.Normalize(alias);
        string? fallback = string.IsNullOrWhiteSpace(fallbackHtml) ? null : fallbackHtml;
        if (normalised.Length == 0)
        {
            return fallback ?? string.Empty;
        }

        if (!_snapshot.Current.Zones.ContainsKey(normalised))
        {
            _recorder.Record(normalised);
            return fallback ?? string.Empty;
        }

        return _writer.Render(new BannerZoneView(normalised, cssClass, _selector.Select(normalised), fallback), requestItems);
    }
}
