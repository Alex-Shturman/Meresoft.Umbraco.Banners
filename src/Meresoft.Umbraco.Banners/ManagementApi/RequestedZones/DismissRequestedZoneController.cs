using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.RequestedZones;

/// <summary>Dismisses a "placed but not set up" zone.</summary>
public class DismissRequestedZoneController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public DismissRequestedZoneController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Removes the alias from the list; it comes back when a template asks for it again.</summary>
    [HttpDelete("requested-zones/{alias}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Dismiss(string alias)
    {
        await _zones.DismissRequestedZoneAsync(alias);
        return NoContent();
    }
}
