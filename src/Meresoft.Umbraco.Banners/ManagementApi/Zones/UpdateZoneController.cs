using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Updates a zone.</summary>
public class UpdateZoneController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public UpdateZoneController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Updates a zone; a stale update date gives 409.</summary>
    [HttpPut("zones/{key:guid}")]
    [ProducesResponseType<ZoneResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid key, ZoneRequestModel model)
    {
        if (model.UpdateDate is null)
        {
            return OperationStatusResults.ToProblem(
                BannerOperationStatus.InvalidData,
                new Dictionary<string, string> { ["updateDate"] = "Send the update date you loaded, so a change by someone else is detected." });
        }

        Attempt<BannerZone, BannerOperationStatus> attempt = await _zones.UpdateAsync(key, model.ToServiceModel(), CurrentUserKey());
        return attempt.Success
            ? Ok(ZoneResponseModel.From(attempt.Result!))
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
