using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Gets one banner with its draft and live versions.</summary>
public class GetBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;
    private readonly BannerResponseMapper _mapper;

    public GetBannerController(IBannerService banners, BannerResponseMapper mapper)
    {
        _banners = banners;
        _mapper = mapper;
    }

    /// <summary>Gets a banner by key.</summary>
    [HttpGet("banners/{key:guid}")]
    [ProducesResponseType<BannerResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid key)
    {
        Banner? banner = await _banners.GetAsync(key);
        return banner is null ? OperationStatusResults.ToProblem(BannerOperationStatus.NotFound) : Ok(await _mapper.MapAsync(banner));
    }
}
