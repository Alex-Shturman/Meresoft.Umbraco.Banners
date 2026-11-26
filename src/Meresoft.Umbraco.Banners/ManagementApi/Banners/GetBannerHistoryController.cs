using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.History;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Gets the audit history of one banner (FR-018).</summary>
public class GetBannerHistoryController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;
    private readonly BannerHistoryReader _history;

    /// <summary>Creates the controller.</summary>
    public GetBannerHistoryController(IBannerService banners, BannerHistoryReader history)
    {
        _banners = banners;
        _history = history;
    }

    /// <summary>Gets a page of the history, newest first.</summary>
    [HttpGet("banners/{key:guid}/history")]
    [ProducesResponseType<HistoryPageModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(Guid key, int skip = 0, int take = HistoryPaging.DefaultTake)
    {
        if (HistoryPaging.Validate(skip, take) is { } invalid)
        {
            return invalid;
        }

        Banner? banner = await _banners.GetAsync(key);
        return banner is null
            ? OperationStatusResults.ToProblem(BannerOperationStatus.NotFound)
            : Ok(await _history.ReadAsync(banner.Id, BannersConstants.BannerEntityType, skip, take));
    }
}
