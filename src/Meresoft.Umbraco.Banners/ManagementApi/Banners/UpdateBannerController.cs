using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Authorization;
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
    private readonly HtmlPermissionChecker _htmlPermission;

    public UpdateBannerController(IBannerService banners, BannerResponseMapper mapper, HtmlPermissionChecker htmlPermission)
    {
        _banners = banners;
        _mapper = mapper;
        _htmlPermission = htmlPermission;
    }

    /// <summary>Saves the draft; a stale update date gives 409.</summary>
    [HttpPut("banners/{key:guid}")]
    [ProducesResponseType<BannerResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid key, BannerRequestModel model)
    {
        if (model.UpdateDate is null)
        {
            return OperationStatusResults.ToProblem(
                BannerOperationStatus.InvalidData,
                new Dictionary<string, string> { ["updateDate"] = "Send the update date you loaded, so a change by someone else is detected." });
        }

        // Only a change of the html value, or a request to make the banner an Html banner, needs the HTML permission
        // (FR-006); saving the other fields of an Html banner, with its html unchanged, does not.
        Banner? stored = await _banners.GetAsync(key);
        if (stored?.Draft is not null
            && (HtmlPermissionChecker.ChangesHtml(stored.Draft.Html, model.Html) || (model.Type == BannerType.Html && stored.Type != BannerType.Html))
            && !_htmlPermission.IsAllowed(User))
        {
            return OperationStatusResults.HtmlPermissionRequired();
        }

        Attempt<Banner, BannerOperationStatus> attempt = await _banners.SaveDraftAsync(key, model.ToDraftModel(), CurrentUserKey());
        return attempt.Success
            ? Ok(await _mapper.MapAsync(attempt.Result!))
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
