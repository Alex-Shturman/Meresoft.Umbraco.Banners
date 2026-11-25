using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Publishes the draft of a banner.</summary>
public class PublishBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;
    private readonly BannerResponseMapper _mapper;

    public PublishBannerController(IBannerService banners, BannerResponseMapper mapper)
    {
        _banners = banners;
        _mapper = mapper;
    }

    /// <summary>Copies the draft over the live version.</summary>
    [HttpPost("banners/{key:guid}/publish")]
    [ProducesResponseType<BannerResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid key, PublishBannerRequestModel model)
    {
        Attempt<Banner, BannerOperationStatus> attempt = await _banners.PublishAsync(key, CurrentUserKey(), model.UpdateDate);
        return attempt.Success
            ? Ok(await _mapper.MapAsync(attempt.Result!))
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
