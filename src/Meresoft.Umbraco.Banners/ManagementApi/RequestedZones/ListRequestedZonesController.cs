using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.RequestedZones;

/// <summary>A zone alias that a template asked for but that does not exist yet.</summary>
/// <param name="Alias">Normalised alias.</param>
/// <param name="FirstSeen">First time asked for, UTC.</param>
/// <param name="LastSeen">Last time asked for, UTC.</param>
public sealed record RequestedZoneResponseModel(string Alias, DateTime FirstSeen, DateTime LastSeen);

/// <summary>Lists the "placed but not set up" zones.</summary>
public class ListRequestedZonesController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public ListRequestedZonesController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Lists the requested zones.</summary>
    [HttpGet("requested-zones")]
    [ProducesResponseType<IEnumerable<RequestedZoneResponseModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        IReadOnlyList<RequestedZone> requested = await _zones.GetRequestedZonesAsync();
        return Ok(requested.Select(r => new RequestedZoneResponseModel(r.Alias, r.FirstSeen, r.LastSeen)).ToList());
    }
}
