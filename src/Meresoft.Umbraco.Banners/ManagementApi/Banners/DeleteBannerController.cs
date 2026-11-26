using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Deletes a banner; its statistics stay.</summary>
public class DeleteBannerController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;

    /// <summary>Creates the controller.</summary>
    public DeleteBannerController(IBannerService banners) => _banners = banners;

    /// <summary>Deletes a banner.</summary>
    [HttpDelete("banners/{key:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid key)
    {
        BannerOperationStatus status = await _banners.DeleteAsync(key, CurrentUserKey());
        return status == BannerOperationStatus.Success ? NoContent() : OperationStatusResults.ToProblem(status);
    }
}
