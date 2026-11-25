using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Gets one zone.</summary>
public class GetZoneController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public GetZoneController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Gets a zone by key.</summary>
    [HttpGet("zones/{key:guid}")]
    [ProducesResponseType<ZoneResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid key)
    {
        BannerZone? zone = await _zones.GetAsync(key);
        return zone is null ? OperationStatusResults.ToProblem(BannerOperationStatus.NotFound) : Ok(ZoneResponseModel.From(zone));
    }
}
