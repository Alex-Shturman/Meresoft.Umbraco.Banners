using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;
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
    private const string ApiRoot = "/umbraco/management/api/v1/" + BannersConstants.ApiName;

    /// <summary>Key of the signed in back office user; the authorization policy guarantees there is one.</summary>
    protected Guid CurrentUserKey() =>
        HttpContext.RequestServices.GetRequiredService<IAuthorizationHelper>().TryGetUmbracoUser(User, out IUser? user)
            ? user.Key
            : throw new InvalidOperationException("The request has no back office user.");

    /// <summary>A 201 response whose Location header points at <c>{collection}/{key}</c> of this API.</summary>
    protected IActionResult CreatedAtKey(string collection, Guid key) =>
        Created($"{Request.PathBase}{ApiRoot}/{collection}/{key}", null);
}
