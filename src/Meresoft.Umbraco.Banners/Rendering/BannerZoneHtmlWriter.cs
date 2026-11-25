using System.Net;
using System.Text;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>Writes the server mode markup of a zone (contracts/markup.md). Every value is HTML encoded.</summary>
public sealed class BannerZoneHtmlWriter
{
    private const string ScriptWrittenKey = "Meresoft.Banners.ScriptWritten";

    private readonly string _basePath;
    private readonly IViewTokenProtector _tokens;

    /// <summary>Creates the writer.</summary>
    public BannerZoneHtmlWriter(IOptions<BannersOptions> options, IViewTokenProtector tokens)
    {
        ArgumentNullException.ThrowIfNull(options);
        _basePath = options.Value.PublicPath;
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    /// <summary>
    /// Writes the zone. With no banner it gives the fallback, or nothing. The package script tag is
    /// added once per request, tracked in <paramref name="requestItems"/> (<c>HttpContext.Items</c>).
    /// </summary>
    public string Render(BannerZoneView view, IDictionary<object, object?> requestItems)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(requestItems);

        if (view.Banners.Count == 0)
        {
            return view.FallbackHtml ?? string.Empty;
        }

        // Everything from here runs for every zone of every page view. Keep the work per render minimal: no database,
        // no allocation beyond the markup, and the token (one data protection call) is the only crypto. Anything that
        // can be prepared when the snapshot is built belongs there, not here.
        var html = new StringBuilder(512);
        html.Append("<div class=\"mb-zone");
        if (!string.IsNullOrWhiteSpace(view.CssClass))
        {
            html.Append(' ').Append(Encode(view.CssClass.Trim()));
        }

        html.Append("\" data-mbz=\"").Append(Encode(view.Alias))
            .Append("\" data-mb-base=\"").Append(Encode(_basePath))
            .Append("\" data-mbz-token=\"").Append(_tokens.Issue(view.Banners[0].ZoneId, [.. view.Banners.Select(b => b.BannerId)]))
            .Append("\">");

        foreach (SelectedBanner banner in view.Banners)
        {
            AppendBanner(html, banner);
        }

        html.Append("</div>");

        if (requestItems.TryAdd(ScriptWrittenKey, true))
        {
            html.Append(PackageScript.Tag);
        }

        return html.ToString();
    }

    private void AppendBanner(StringBuilder html, SelectedBanner banner)
    {
        if (banner.Html is not null)
        {
            // Html banners are written as stored, in the box the package script measures; the package cannot wrap
            // foreign code in a click redirect (FR-006). Only editors with the HTML permission can store this markup.
            html.Append("<div class=\"mb-banner mb-html\" data-mbb=\"").Append(banner.BannerId).Append("\">").Append(banner.Html).Append("</div>");
            return;
        }

        html.Append("<div class=\"mb-banner\" data-mbb=\"").Append(banner.BannerId).Append("\">");
        // The link goes through the click endpoint, which counts the click and redirects to the stored address.
        html.Append("<a href=\"").Append(Encode($"{_basePath}/c/{banner.BannerId}?z={banner.ZoneId}")).Append("\" rel=\"sponsored noopener\"");
        if (banner.OpenInNewWindow)
        {
            html.Append(" target=\"_blank\"");
        }

        if (!string.IsNullOrEmpty(banner.Title))
        {
            html.Append(" title=\"").Append(Encode(banner.Title)).Append('"');
        }

        html.Append("><img src=\"").Append(Encode(banner.ImageUrl)).Append("\" alt=\"").Append(Encode(banner.AltText)).Append('"');
        if (banner.Width is { } width)
        {
            html.Append(" width=\"").Append(width).Append('"');
        }

        if (banner.Height is { } height)
        {
            html.Append(" height=\"").Append(height).Append('"');
        }

        html.Append(" loading=\"lazy\" decoding=\"async\"></a></div>");
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
