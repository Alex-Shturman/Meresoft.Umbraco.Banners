using System.Globalization;
using Meresoft.Umbraco.Banners.Counting;
using Microsoft.AspNetCore.Authorization;
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

        // The user agent is only read here, in memory, for the bot check.
        if (target.Countable && zoneId is { } z && !_bots.IsBot(Request.Headers.UserAgent.ToString()))
        {
            _buffer.AddClick(DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime), banner, z);
        }

        return Redirect(target.LinkUrl);
    }
}
