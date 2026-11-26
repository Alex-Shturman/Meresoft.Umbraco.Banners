using System.Net;
using Meresoft.Umbraco.Banners.ManagementApi;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Integration.ManagementApi;
using Umbraco.Cms.Tests.Common.Testing;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

/// <summary>A controller that exists only in tests, to exercise the base class policy (research R4).</summary>
public sealed class SectionProbeController : BannersManagementApiControllerBase
{
    [HttpGet("section-probe")]
    public IActionResult Probe() => Ok("ok");
}

[TestFixture]
public class SectionAccessTests : ManagementApiTest<SectionProbeController>
{
    protected override System.Linq.Expressions.Expression<Func<SectionProbeController, object>> MethodSelector
    {
        get => x => x.Probe();
        set { }
    }

    private const string ProbeUrl = "/umbraco/management/api/v1/meresoft-banners/section-probe";

    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder) =>
        mvcBuilder.AddApplicationPart(typeof(SectionProbeController).Assembly);

    [SetUp]
    public async Task WaitForRuntime() => await RuntimeReady.WaitForRunLevelAsync(GetRequiredService<IRuntimeState>());

    [Test]
    public async Task Anonymous_Gets401()
    {
        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UserWithoutTheBannersSection_Gets403()
    {
        await AuthenticateWithSectionsAsync("noBannersGroup", [Constants.Applications.Content]);

        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UserWithTheBannersSection_Gets200()
    {
        await AuthenticateWithSectionsAsync("bannersGroup", [BannersConstants.SectionAlias]);

        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Administrator_Gets200_BecauseTheMigrationGrantedTheSection()
    {
        await AuthenticateClientAsync(Client, "admin-probe@test.invalid", "1234567890", Constants.Security.AdminGroupKey);

        // The test helper only puts the user into the built-in group; the section comes from the package migration.
        IUserGroup admins = (await GetRequiredService<IUserGroupService>().GetAsync(Constants.Security.AdminGroupKey))!;
        Assert.That(admins.AllowedSections, Does.Contain(BannersConstants.SectionAlias), "granted by GrantAdministratorsAccess");

        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Administrator_Gets403_WhenTheGrantIsRemoved_SoTheHelperGrantsNothing()
    {
        IUserGroupService groups = GetRequiredService<IUserGroupService>();
        IUserGroup admins = (await groups.GetAsync(Constants.Security.AdminGroupKey))!;
        admins.RemoveAllowedSection(BannersConstants.SectionAlias);
        await groups.UpdateAsync(admins, Constants.Security.SuperUserKey);

        await AuthenticateClientAsync(Client, "admin-probe-2@test.invalid", "1234567890", Constants.Security.AdminGroupKey);
        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
