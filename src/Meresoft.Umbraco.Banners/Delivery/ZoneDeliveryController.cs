using System.Text.Json;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Delivery.Models;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// <c>GET {PublicPath}/zone/{alias}</c>: client mode delivery. The package script of a cached page asks for the banners
/// of a zone and gets public fields only, with a view token the <c>seen</c> endpoint accepts. Reads memory only.
/// </summary>
[BannersPublicRoute]
[AllowAnonymous]
public sealed class ZoneDeliveryController : ControllerBase
{
    private readonly IBannerSelector _selector;
    private readonly ILiveSnapshotAccessor _snapshot;
    private readonly IViewTokenProtector _tokens;
    private readonly bool _enabled;
    private readonly string _basePath;

    /// <summary>Creates the controller.</summary>
    public ZoneDeliveryController(
        IBannerSelector selector,
        ILiveSnapshotAccessor snapshot,
        IViewTokenProtector tokens,
        IOptions<BannersOptions> options)
    {
        _selector = selector;
        _snapshot = snapshot;
        _tokens = tokens;
        _enabled = options.Value.DeliveryEndpointEnabled;
        _basePath = options.Value.PublicPath;
    }

    /// <summary>The banners of a zone, chosen now.</summary>
    [HttpGet("zone/{alias}")]
    public IActionResult Zone(string alias)
    {
        if (!_enabled)
        {
            return NotFound();
        }

        string normalised = AliasNormalizer.Normalize(alias);
        if (!ZoneValidator.IsValidAlias(normalised))
        {
            return NotFound();
        }

        // An unknown zone is answered like an empty one. It is not noted as requested: the tag helper that writes every
        // client placeholder already notes missing zones (FR-003), and an anonymous caller must not be able to fill the
        // capped set of requested zones with junk aliases.
        if (!_snapshot.Current.Zones.ContainsKey(normalised))
        {
            return Answer(new ZoneDeliveryResponse(normalised, null, []));
        }

        IReadOnlyList<SelectedBanner> chosen = _selector.Select(normalised);
        if (chosen.Count == 0)
        {
            return Answer(new ZoneDeliveryResponse(normalised, null, []));
        }

        string token = _tokens.Issue(chosen[0].ZoneId, [.. chosen.Select(b => b.BannerId)]);
        return Answer(new ZoneDeliveryResponse(normalised, token, [.. chosen.Select(ToDelivered)]));
    }

    private DeliveredBanner ToDelivered(SelectedBanner banner) =>
        banner.Html is not null
            ? new DeliveredHtmlBanner(banner.BannerId, banner.Html)
            : new DeliveredImageBanner(
                banner.BannerId,
                $"{Request.PathBase.ToUriComponent()}{_basePath}/c/{banner.BannerId}?z={banner.ZoneId}",
                banner.OpenInNewWindow,
                banner.ImageUrl,
                banner.Width,
                banner.Height,
                banner.AltText,
                string.IsNullOrEmpty(banner.Title) ? null : banner.Title);

    private ContentResult Answer(ZoneDeliveryResponse response) =>
        Content(JsonSerializer.Serialize(response, ZoneDeliveryJsonContext.Default.ZoneDeliveryResponse), "application/json");
}
