using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Meresoft.Umbraco.Banners.ManagementApi.Authorization;

/// <summary>
/// Succeeds when the signed in back office user may open the Banners section. Umbraco's own
/// section requirement is internal, so the package checks the user's allowed sections itself (research R4).
/// </summary>
public sealed class SectionAccessHandler : AuthorizationHandler<SectionAccessRequirement>
{
    private readonly IAuthorizationHelper _authorizationHelper;

    /// <summary>Creates the handler.</summary>
    public SectionAccessHandler(IAuthorizationHelper authorizationHelper)
    {
        _authorizationHelper = authorizationHelper;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SectionAccessRequirement requirement)
    {
        if (_authorizationHelper.TryGetUmbracoUser(context.User, out IUser? user)
            && user.AllowedSections.Contains(BannersConstants.SectionAlias))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
