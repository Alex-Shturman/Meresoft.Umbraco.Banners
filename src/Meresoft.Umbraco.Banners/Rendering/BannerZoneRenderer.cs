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

    /// <summary>
    /// First half of a render: normalises the alias once and chooses the banners. A zone that does not exist yet
    /// is noted as requested. Reads memory only.
    /// </summary>
    public ZoneSelection Select(string? alias)
    {
        string normalised = AliasNormalizer.Normalize(alias);
        if (normalised.Length == 0)
        {
            return new ZoneSelection(normalised, []);
        }

        if (!_snapshot.Current.Zones.ContainsKey(normalised))
        {
            _recorder.Record(normalised);
            return new ZoneSelection(normalised, []);
        }

        return new ZoneSelection(normalised, _selector.Select(normalised));
    }

    /// <summary>
    /// Second half of a render: the markup of the selected banners, or the fallback (or nothing) when there are none.
    /// The caller needs the fallback only when <see cref="ZoneSelection.Banners"/> is empty.
    /// </summary>
    public string Render(IDictionary<object, object?> requestItems, ZoneSelection selection, string? cssClass, string? fallbackHtml)
    {
        string? fallback = string.IsNullOrWhiteSpace(fallbackHtml) ? null : fallbackHtml;
        if (selection.Banners.Count == 0)
        {
            return fallback ?? string.Empty;
        }

        return _writer.Render(new BannerZoneView(selection.Alias, cssClass, selection.Banners, fallback), requestItems);
    }
}

/// <summary>The outcome of the selection half of a render.</summary>
/// <param name="Alias">The normalised alias.</param>
/// <param name="Banners">The chosen banners; empty when the zone is unknown, empty, or has no active banner.</param>
public sealed record ZoneSelection(string Alias, IReadOnlyList<SelectedBanner> Banners);
