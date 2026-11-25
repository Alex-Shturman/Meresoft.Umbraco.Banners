using Meresoft.Umbraco.Banners.Composing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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
    /// <summary>The clock of the package services in tests; move it to separate saves by more than the 10 ms timestamp resolution.</summary>
    protected FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero));

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        builder.AddMeresoftBanners();
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.Configure<UnattendedSettings>(options => options.PackageMigrationsUnattended = false);
    }

    /// <summary>Runs the package migration plan (a fresh install of the package).</summary>
    protected async Task InstallPackageAsync()
    {
        PackageMigrationRunner runner = GetRequiredService<PackageMigrationRunner>();
        await runner.RunPackageMigrationsIfPendingAsync(BannersConstants.MigrationPlanName);
    }
}
