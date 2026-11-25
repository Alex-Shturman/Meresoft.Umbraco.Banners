using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Saves the draft of a banner; the site does not change until publish.</summary>
public class UpdateBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;
    private readonly BannerResponseMapper _mapper;

    public UpdateBannerController(IBannerService banners, BannerResponseMapper mapper)
    {
        _banners = banners;
        _mapper = mapper;
    }

    /// <summary>Saves the draft; a stale update date gives 409.</summary>
    [HttpPut("banners/{key:guid}")]
    [ProducesResponseType<BannerResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid key, BannerRequestModel model)
    {
        Attempt<Banner, BannerOperationStatus> attempt = await _banners.SaveDraftAsync(key, model.ToDraftModel(), CurrentUserKey());
        return attempt.Success
            ? Ok(await _mapper.MapAsync(attempt.Result!))
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
