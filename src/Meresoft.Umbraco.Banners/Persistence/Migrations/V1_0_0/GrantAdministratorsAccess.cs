using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
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
            // Fail loudly: if the step succeeded the plan state would advance and the grant would never be retried.
            throw new InvalidOperationException("The Administrators group was not found, so the Banners section could not be granted.");
        }

        administrators.AddAllowedSection(BannersConstants.SectionAlias);
        administrators.Permissions.Add(BannersConstants.HtmlPermission);
        Attempt<IUserGroup, UserGroupOperationStatus> result =
            await _userGroupService.UpdateAsync(administrators, Constants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Granting the Banners section to the Administrators group failed: {result.Status}.");
        }
    }
}
