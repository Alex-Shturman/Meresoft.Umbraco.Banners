using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.V1_0_1;

/// <summary>
/// Gives the Administrators group the HTML banner permission on an install that ran the first grant before that
/// permission existed. It runs once, only changes a group that already has the Banners section (an administrator
/// who removed the section chose to leave the package alone), and does nothing when the verb is already there, so
/// running it again is harmless (FR-017).
/// </summary>
public sealed class GrantAdministratorsHtmlPermission : AsyncMigrationBase
{
    private readonly IUserGroupService _userGroupService;

    /// <summary>Creates the step.</summary>
    public GrantAdministratorsHtmlPermission(IMigrationContext context, IUserGroupService userGroupService)
        : base(context)
    {
        _userGroupService = userGroupService;
    }

    /// <inheritdoc />
    protected override async Task MigrateAsync()
    {
        IUserGroup? administrators = await _userGroupService.GetAsync(Constants.Security.AdminGroupKey);
        if (administrators is null
            || !administrators.AllowedSections.Contains(BannersConstants.SectionAlias)
            || administrators.Permissions.Contains(BannersConstants.HtmlPermission))
        {
            return;
        }

        administrators.Permissions.Add(BannersConstants.HtmlPermission);
        Attempt<IUserGroup, UserGroupOperationStatus> result =
            await _userGroupService.UpdateAsync(administrators, Constants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Granting the HTML banner permission to the Administrators group failed: {result.Status}.");
        }
    }
}
