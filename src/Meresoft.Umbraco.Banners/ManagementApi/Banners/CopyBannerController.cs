using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Copies a banner into a new draft.</summary>
public class CopyBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;

    /// <summary>Creates the controller.</summary>
    public CopyBannerController(IBannerService banners) => _banners = banners;

    /// <summary>Creates a new draft banner from the draft of this one, without statistics.</summary>
    [HttpPost("banners/{key:guid}/copy")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Copy(Guid key, CopyBannerRequestModel model)
    {
        Attempt<Banner, BannerOperationStatus> attempt = await _banners.CopyAsync(key, model.Name, CurrentUserKey());
        return attempt.Success
            ? CreatedAtKey("banners", attempt.Result!.Key)
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
