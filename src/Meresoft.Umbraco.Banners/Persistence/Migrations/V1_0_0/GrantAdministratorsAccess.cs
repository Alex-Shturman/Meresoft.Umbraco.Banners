using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.V1_0_0;

/// <summary>
/// Gives the Administrators group the Banners section and the HTML permission. This runs once,
/// with the first install, and is never repeated: a grant an administrator removes later stays
/// removed (FR-017).
/// </summary>
public sealed class GrantAdministratorsAccess : AsyncMigrationBase
{
    private readonly IUserGroupService _userGroupService;

    /// <summary>Creates the step.</summary>
    public GrantAdministratorsAccess(IMigrationContext context, IUserGroupService userGroupService)
        : base(context)
    {
        _userGroupService = userGroupService;
    }

    /// <inheritdoc />
    protected override async Task MigrateAsync()
    {
        IUserGroup? administrators = await _userGroupService.GetAsync(Constants.Security.AdminGroupKey);
        if (administrators is null)
        {
            Logger.LogWarning("The Administrators group was not found; the Banners section was not granted.");
            return;
        }

        administrators.AddAllowedSection(BannersConstants.SectionAlias);
        administrators.Permissions.Add(BannersConstants.HtmlPermission);
        await _userGroupService.UpdateAsync(administrators, Constants.Security.SuperUserKey);
    }
}
