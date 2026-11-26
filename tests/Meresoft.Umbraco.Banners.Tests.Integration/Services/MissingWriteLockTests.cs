using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// If the package migration has not run (or its lock row was removed), every write of the package would fail on a missing
/// lock row. It must fail with a message that says what is wrong and what to do, and it must write nothing.
/// </summary>
[TestFixture]
public class MissingWriteLockTests : BannersServiceTestBase
{
    private void RemoveTheLockRow()
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Execute("DELETE FROM umbracoLock WHERE id = @0", BannersConstants.WriteLockId);
        scope.Complete();
    }

    [Test]
    public void CreatingAZone_WithoutTheLockRow_FailsWithAMessageThatNamesTheMigration()
    {
        RemoveTheLockRow();

        InvalidOperationException? error = Assert.ThrowsAsync<InvalidOperationException>(
            () => ZoneService.CreateAsync(new BannerZoneModel { Alias = "home-top", Name = "Home", ShowCount = 1 }, User));

        Assert.That(error!.Message, Does.Contain("migration").IgnoreCase.And.Contain("Meresoft.Umbraco.Banners"));
        Assert.That(error.Message, Does.Contain(BannersConstants.WriteLockId.ToString()));
    }

    [Test]
    public async Task NothingIsWritten_WhenTheLockRowIsMissing()
    {
        RemoveTheLockRow();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => ZoneService.CreateAsync(new BannerZoneModel { Alias = "home-top", Name = "Home", ShowCount = 1 }, User));

        Assert.That(await ZoneService.GetAllAsync(), Is.Empty);
    }

    [Test]
    public async Task WithTheLockRow_WritesWork_AsBefore()
    {
        var attempt = await ZoneService.CreateAsync(new BannerZoneModel { Alias = "home-top", Name = "Home", ShowCount = 1 }, User);

        Assert.That(attempt.Success, Is.True);
    }
}
