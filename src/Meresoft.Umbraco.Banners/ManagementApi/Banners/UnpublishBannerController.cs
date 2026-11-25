using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Stops showing a banner.</summary>
public class UnpublishBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;
    private readonly BannerResponseMapper _mapper;

    public UnpublishBannerController(IBannerService banners, BannerResponseMapper mapper)
    {
        _banners = banners;
        _mapper = mapper;
    }

    /// <summary>Unpublishes; the live version stays.</summary>
    [HttpPost("banners/{key:guid}/unpublish")]
    [ProducesResponseType<BannerResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpublish(Guid key)
    {
        Attempt<Banner, BannerOperationStatus> attempt = await _banners.UnpublishAsync(key, CurrentUserKey());
        return attempt.Success
            ? Ok(await _mapper.MapAsync(attempt.Result!))
            : OperationStatusResults.ToProblem(attempt.Status);
    }
}
