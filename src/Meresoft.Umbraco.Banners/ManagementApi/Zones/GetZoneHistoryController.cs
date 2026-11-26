using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.History;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Gets the audit history of one zone (FR-018).</summary>
public class GetZoneHistoryController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;
    private readonly BannerHistoryReader _history;

    /// <summary>Creates the controller.</summary>
    public GetZoneHistoryController(IBannerZoneService zones, BannerHistoryReader history)
    {
        _zones = zones;
        _history = history;
    }

    /// <summary>Gets a page of the history, newest first.</summary>
    [HttpGet("zones/{key:guid}/history")]
    [ProducesResponseType<HistoryPageModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid key, int skip = 0, int take = HistoryPaging.DefaultTake)
    {
        if (HistoryPaging.Validate(skip, take) is { } invalid)
        {
            return invalid;
        }

        BannerZone? zone = await _zones.GetAsync(key);
        return zone is null
            ? OperationStatusResults.ToProblem(BannerOperationStatus.NotFound)
            : Ok(await _history.ReadAsync(zone.Id, BannersConstants.ZoneEntityType, skip, take));
    }
}
