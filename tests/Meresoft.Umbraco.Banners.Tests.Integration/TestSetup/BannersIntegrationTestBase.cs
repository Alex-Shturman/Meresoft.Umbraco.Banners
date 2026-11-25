using Meresoft.Umbraco.Banners.Composing;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Install;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Base for integration tests that use the package on a real Umbraco instance (SQLite by default).
/// Package migrations are not run automatically, so each test decides when the schema is installed.
/// </summary>
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public abstract class BannersIntegrationTestBase : UmbracoIntegrationTest
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        builder.AddMeresoftBanners();
        builder.Services.Configure<UnattendedSettings>(options => options.PackageMigrationsUnattended = false);
    }

    /// <summary>Runs the package migration plan (a fresh install of the package).</summary>
    protected async Task InstallPackageAsync()
    {
        PackageMigrationRunner runner = GetRequiredService<PackageMigrationRunner>();
        await runner.RunPackageMigrationsIfPendingAsync(BannersConstants.MigrationPlanName);
    }
}
