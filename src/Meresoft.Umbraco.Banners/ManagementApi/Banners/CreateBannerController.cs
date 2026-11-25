using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Creates a banner in the Draft state.</summary>
public class CreateBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;

    /// <summary>Creates the controller.</summary>
    public CreateBannerController(IBannerService banners) => _banners = banners;

    /// <summary>Creates a banner.</summary>
    [HttpPost("banners")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(BannerRequestModel model)
    {
        // The service refuses an Html banner or any html value without the HTML permission (403 html-permission-required).
        var create = new BannerCreateModel { Type = model.Type ?? BannerType.Image, Draft = model.ToDraftModel() };
        Attempt<Banner, BannerOperationStatus> attempt = await _banners.CreateAsync(create, CurrentUserKey());
        return attempt.Success
            ? CreatedAtKey("banners", attempt.Result!.Key)
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
