using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Creates a zone.</summary>
public class CreateZoneController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public CreateZoneController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Creates a zone; removes the matching requested zone.</summary>
    [HttpPost("zones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ZoneRequestModel model)
    {
        Attempt<BannerZone, BannerOperationStatus> attempt = await _zones.CreateAsync(model.ToServiceModel(), CurrentUserKey());
        return attempt.Success
            ? CreatedAtKey("zones", attempt.Result!.Key)
            : OperationStatusResults.ToProblem(attempt.Status, (attempt.Exception as BannerValidationException)?.Errors);
    }
}
