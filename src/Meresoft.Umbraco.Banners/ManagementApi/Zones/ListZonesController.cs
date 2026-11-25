using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Lists the zones with the number of banners shown in each.</summary>
public class ListZonesController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;
    private readonly ILiveSnapshotAccessor _snapshot;

    /// <summary>Creates the controller.</summary>
    public ListZonesController(IBannerZoneService zones, ILiveSnapshotAccessor snapshot)
    {
        _zones = zones;
        _snapshot = snapshot;
    }

    /// <summary>Lists the zones.</summary>
    [HttpGet("zones")]
    [ProducesResponseType<IEnumerable<ZoneResponseModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List()
    {
        LiveSnapshot snapshot = _snapshot.Current;
        IReadOnlyList<BannerZone> zones = await _zones.GetAllAsync();
        return Ok(zones.Select(z => ZoneResponseModel.From(
            z,
            snapshot.Zones.TryGetValue(z.Alias, out SnapshotZone? live) ? live.Candidates.Count : 0)));
    }
}
