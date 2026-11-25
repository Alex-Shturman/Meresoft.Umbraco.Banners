using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;

namespace Meresoft.Umbraco.Banners.ManagementApi;

/// <summary>
/// Base of every management API controller of the package: routed under the Umbraco management
/// API, part of the package's own OpenAPI document, and open only to users with the Banners section.
/// </summary>
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute(BannersConstants.ApiName)]
[ApiExplorerSettings(GroupName = "Banners")]
[MapToApi(BannersConstants.ApiName)]
[Authorize(Policy = BannersConstants.SectionAccessPolicy)]
public abstract class BannersManagementApiControllerBase : ManagementApiControllerBase
{
}
