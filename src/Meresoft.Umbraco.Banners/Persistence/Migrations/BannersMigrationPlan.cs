using Meresoft.Umbraco.Banners.Persistence.Migrations.V1_0_0;
using Umbraco.Cms.Core.Packaging;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations;

/// <summary>
/// The migration plan of the package. Version 1.0.0 creates the six tables and grants the
/// Administrators group access to the section; later steps never touch user groups.
/// </summary>
public class BannersMigrationPlan : PackageMigrationPlan
{
    /// <summary>Creates the plan.</summary>
    public BannersMigrationPlan()
        : base(BannersConstants.MigrationPlanName)
    {
    }

    /// <inheritdoc />
    protected override void DefinePlan()
    {
        To<CreateTables>("7b0c6c47-8f50-4f35-9a52-5f1d2a8f6a01");
        To<GrantAdministratorsAccess>("c9a4f1d2-3b8e-4e6a-b7d1-2e5f9c3a7b02");
    }
}
