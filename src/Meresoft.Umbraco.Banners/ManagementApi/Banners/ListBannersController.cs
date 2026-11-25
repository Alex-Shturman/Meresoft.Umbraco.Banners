using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Lists banners with filters and paging.</summary>
public class ListBannersController : BannersManagementApiControllerBase
{
    private readonly IBannerService _banners;

    /// <summary>Creates the controller.</summary>
    public ListBannersController(IBannerService banners) => _banners = banners;

    /// <summary>Lists a page of banners, newest change first.</summary>
    [HttpGet("banners")]
    [ProducesResponseType<BannerPageModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(Guid? zone = null, BannerState? state = null, BannerType? type = null, int skip = 0, int take = 50)
    {
        if (skip < 0 || take is < 1 or > BannerQuery.MaxTake)
        {
            return OperationStatusResults.ToProblem(
                BannerOperationStatus.InvalidData,
                new Dictionary<string, string> { [take is < 1 or > BannerQuery.MaxTake ? "take" : "skip"] = $"Skip must be 0 or more and take 1 to {BannerQuery.MaxTake}." });
        }

        PagedModel<BannerListItem> page = await _banners.GetPagedAsync(
            new BannerQuery { ZoneKey = zone, State = state, Type = type, Skip = skip, Take = take });

        return Ok(new BannerPageModel
        {
            Total = page.Total,
            Items = page.Items.Select(i => new BannerListItemModel
            {
                Key = i.Key,
                Name = i.Name,
                Type = i.Type,
                State = i.State,
                Flags = BannerFlagsModel.From(i.Flags),
                ZoneKeys = i.ZoneKeys,
                Weight = i.Weight,
                UpdateDate = i.UpdateDate,
            }).ToList(),
        });
    }
}
