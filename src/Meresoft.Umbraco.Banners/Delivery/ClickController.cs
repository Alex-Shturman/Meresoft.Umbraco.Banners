using System.Globalization;
using Meresoft.Umbraco.Banners.Counting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// <c>GET {PublicPath}/c/{bannerId}?z={zoneId}</c>: counts a click and redirects to the link stored for the banner.
/// The target is never taken from the request, so there is no open redirect (contracts/public-endpoints.md).
/// </summary>
[BannersPublicRoute]
[AllowAnonymous]
public sealed class ClickController : ControllerBase
{
    private readonly IBannerLinkLookup _links;
    private readonly BotMatcher _bots;
    private readonly CounterBuffer _buffer;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the controller.</summary>
    public ClickController(IBannerLinkLookup links, BotMatcher bots, CounterBuffer buffer, TimeProvider timeProvider)
    {
        _links = links;
        _bots = bots;
        _buffer = buffer;
        _timeProvider = timeProvider;
    }

    /// <summary>Redirects to the stored link of the banner.</summary>
    [HttpGet("c/{bannerId}")]
    public IActionResult Click(string bannerId, [FromQuery(Name = "z")] string? zone)
    {
        // Ids are plain digits; anything else is not a banner of ours.
        if (!int.TryParse(bannerId, NumberStyles.None, CultureInfo.InvariantCulture, out int banner))
        {
            return NotFound();
        }

        int? zoneId = null;
        if (!string.IsNullOrEmpty(zone))
        {
            if (!int.TryParse(zone, NumberStyles.None, CultureInfo.InvariantCulture, out int parsedZone))
            {
                return NotFound();
            }

            zoneId = parsedZone;
        }

        ClickTarget? target = _links.Find(banner, zoneId);
        if (target is null)
        {
            return NotFound();
        }

        // The user agent and the fetch headers are only read here, in memory, for the bot and navigation checks.
        if (target.Countable && zoneId is { } z && IsNavigation() && !_bots.IsBot(Request.Headers.UserAgent.ToString()))
        {
            _buffer.AddClick(DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime), banner, z);
        }

        return Redirect(target.LinkUrl);
    }

    /// <summary>
    /// True for a person following the link: the browser says it is a navigation to a document, or sends no fetch metadata
    /// at all (older browsers). Fetch and image requests, and prefetches, are redirected but not counted.
    /// </summary>
    private bool IsNavigation()
    {
        IHeaderDictionary headers = Request.Headers;
        string mode = headers["Sec-Fetch-Mode"].ToString();
        string destination = headers["Sec-Fetch-Dest"].ToString();
        return (mode.Length == 0 || mode.Equals("navigate", StringComparison.OrdinalIgnoreCase))
            && (destination.Length == 0 || destination.Equals("document", StringComparison.OrdinalIgnoreCase))
            && !headers["Sec-Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)
            && !headers["Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase);
    }
}
