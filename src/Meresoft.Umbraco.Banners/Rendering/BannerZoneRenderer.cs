using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
using Microsoft.AspNetCore.Http;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>Chooses and writes a zone for the tag helper and the view component. Reads memory only.</summary>
public sealed class BannerZoneRenderer
{
    private readonly IBannerSelector _selector;
    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly RequestedZoneRecorder _recorder;
    /// <summary>How long a render waits for the start up build of the snapshot, at most.</summary>
    internal static readonly TimeSpan StartupWait = TimeSpan.FromSeconds(2);

    private const string WaitTimedOutKey = "Meresoft.Banners.StartupWaitTimedOut";

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

        if (!_snapshot.Current.Zones.TryGetValue(normalised, out SnapshotZone? zone))
        {
            _recorder.Record(normalised);
            return new ZoneSelection(normalised, []);
        }

        // A client mode zone chooses nothing here: the browser asks the delivery endpoint, so the page can be cached.
        return zone.RenderMode == ZoneRenderMode.Client
            ? new ZoneSelection(normalised, [], ZoneRenderMode.Client)
            : new ZoneSelection(normalised, _selector.Select(normalised));
    }

    /// <summary>
    /// Same as <see cref="Select"/>, but safe before the first snapshot build: it waits for the start up build, at most
    /// 2 seconds for the whole request, and when the snapshot is still not built it answers empty without noting the zone as
    /// requested and marks the response <c>Cache-Control: no-store</c> (if its headers have not started), so a cache cannot
    /// keep the empty answer. Memory only when the snapshot is built.
    /// </summary>
    public async ValueTask<ZoneSelection> SelectAsync(string? alias, HttpContext? httpContext)
    {
        if (_snapshot.IsBuilt)
        {
            return Select(alias);
        }

        bool timedOutBefore = httpContext?.Items.ContainsKey(WaitTimedOutKey) == true;
        bool built = !timedOutBefore
            && await _snapshot.WaitForFirstBuildAsync(StartupWait, httpContext?.RequestAborted ?? CancellationToken.None);
        if (built || _snapshot.IsBuilt)
        {
            return Select(alias);
        }

        if (httpContext is not null)
        {
            httpContext.Items[WaitTimedOutKey] = true;
            if (!httpContext.Response.HasStarted)
            {
                httpContext.Response.Headers.CacheControl = "no-store";
            }
        }

        return new ZoneSelection(AliasNormalizer.Normalize(alias), []);
    }

    /// <summary>
    /// Second half of a render: the markup of the selected banners, or the fallback (or nothing) when there are none.
    /// The caller needs the fallback only when <see cref="ZoneSelection.Banners"/> is empty.
    /// </summary>
    public string Render(IDictionary<object, object?> requestItems, ZoneSelection selection, string? cssClass, string? fallbackHtml)
    {
        string? fallback = string.IsNullOrWhiteSpace(fallbackHtml) ? null : fallbackHtml;
        if (selection.Mode == ZoneRenderMode.Client)
        {
            return _writer.Render(new BannerZoneView(selection.Alias, cssClass, [], fallback, ZoneRenderMode.Client), requestItems);
        }

        if (selection.Banners.Count == 0)
        {
            return fallback ?? string.Empty;
        }

        return _writer.Render(new BannerZoneView(selection.Alias, cssClass, selection.Banners, fallback), requestItems);
    }
}

/// <summary>The outcome of the selection half of a render.</summary>
/// <param name="Alias">The normalised alias.</param>
/// <param name="Banners">The chosen banners; empty when the zone is unknown, empty, has no active banner, or is a client mode zone.</param>
/// <param name="Mode">Client for a zone the browser fills; Server otherwise (also for an unknown zone).</param>
public sealed record ZoneSelection(string Alias, IReadOnlyList<SelectedBanner> Banners, ZoneRenderMode Mode = ZoneRenderMode.Server);
