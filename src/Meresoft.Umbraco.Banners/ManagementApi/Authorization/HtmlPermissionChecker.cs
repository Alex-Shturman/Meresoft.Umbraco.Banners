using System.Security.Claims;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Meresoft.Umbraco.Banners.ManagementApi.Authorization;

/// <summary>
/// Decides whether the signed in back office user may create or change HTML. The permission is stored as a
/// fallback permission of a user group, so it is granted when any of the user's groups has the verb (research R4).
/// </summary>
public sealed class HtmlPermissionChecker
{
    private readonly IAuthorizationHelper _authorizationHelper;

    /// <summary>Creates the checker.</summary>
    public HtmlPermissionChecker(IAuthorizationHelper authorizationHelper)
    {
        _authorizationHelper = authorizationHelper ?? throw new ArgumentNullException(nameof(authorizationHelper));
    }

    /// <summary>True when any group of the user has <see cref="BannersConstants.HtmlPermission"/>.</summary>
    public bool IsAllowed(ClaimsPrincipal principal) =>
        _authorizationHelper.TryGetUmbracoUser(principal, out IUser? user)
        && user.Groups.Any(group => group.Permissions.Contains(BannersConstants.HtmlPermission));

    /// <summary>
    /// True when a save changes the HTML. An empty value and a missing one are the same, so a client that sends
    /// null for an image banner is not asked for the permission.
    /// </summary>
    public static bool ChangesHtml(string? storedHtml, string? requestedHtml) =>
        !string.Equals(storedHtml ?? string.Empty, requestedHtml ?? string.Empty, StringComparison.Ordinal);
}
