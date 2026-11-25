using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Services.Authorization;

/// <summary>
/// Decides whether a back office user may create or change HTML. The permission is stored as a fallback permission
/// of a user group, so it is granted when any of the user's groups has the verb (research R4). The banner service
/// asks it for every write that can set or change HTML, so no caller can bypass it.
/// </summary>
public sealed class HtmlPermissionChecker
{
    private readonly IUserService _userService;

    /// <summary>Creates the checker.</summary>
    public HtmlPermissionChecker(IUserService userService)
    {
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    /// <summary>True when any group of the user has <see cref="BannersConstants.HtmlPermission"/>; false for an unknown user.</summary>
    public async Task<bool> IsAllowedAsync(Guid userKey)
    {
        var user = await _userService.GetAsync(userKey);
        return user is not null && user.Groups.Any(group => group.Permissions.Contains(BannersConstants.HtmlPermission));
    }

    /// <summary>
    /// True when a save changes the HTML. An empty value and a missing one are the same, so a client that sends
    /// null for an image banner is not asked for the permission.
    /// </summary>
    public static bool ChangesHtml(string? storedHtml, string? requestedHtml) =>
        !string.Equals(storedHtml ?? string.Empty, requestedHtml ?? string.Empty, StringComparison.Ordinal);
}
