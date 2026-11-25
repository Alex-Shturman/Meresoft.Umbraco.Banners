using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.History;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Gets the audit history of one zone (FR-018).</summary>
public class GetZoneHistoryController : BannersManagementApiControllerBase
{
    private const int MaxTake = 100;

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
    public async Task<IActionResult> GetHistory(Guid key, int skip = 0, int take = 50)
    {
        if (skip is < 0 or > BannerHistoryReader.MaxSkip || take is < 1 or > MaxTake)
        {
            return OperationStatusResults.ToProblem(
                BannerOperationStatus.InvalidData,
                new Dictionary<string, string> { [take is < 1 or > MaxTake ? "take" : "skip"] = $"Skip must be 0 to {BannerHistoryReader.MaxSkip} and take 1 to {MaxTake}." });
        }

        BannerZone? zone = await _zones.GetAsync(key);
        return zone is null
            ? OperationStatusResults.ToProblem(BannerOperationStatus.NotFound)
            : Ok(await _history.ReadAsync(zone.Id, BannersConstants.ZoneEntityType, skip, take));
    }
}
