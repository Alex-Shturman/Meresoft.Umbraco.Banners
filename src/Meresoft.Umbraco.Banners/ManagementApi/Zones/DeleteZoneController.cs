using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Zones;

/// <summary>Deletes a zone.</summary>
public class DeleteZoneController : BannersManagementApiControllerBase
{
    private readonly IBannerZoneService _zones;

    /// <summary>Creates the controller.</summary>
    public DeleteZoneController(IBannerZoneService zones) => _zones = zones;

    /// <summary>Deletes a zone; 409 with the assigned banner keys while banners use it.</summary>
    [HttpDelete("zones/{key:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid key)
    {
        BannerOperationStatus status = await _zones.DeleteAsync(key, CurrentUserKey());
        return status switch
        {
            BannerOperationStatus.Success => NoContent(),
            BannerOperationStatus.ZoneInUse => OperationStatusResults.ToProblem(status, null, await _zones.GetAssignedBannerKeysAsync(key)),
            _ => OperationStatusResults.ToProblem(status),
        };
    }
}
