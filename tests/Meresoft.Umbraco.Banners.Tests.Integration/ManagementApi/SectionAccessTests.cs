using System.Net;
using Meresoft.Umbraco.Banners.ManagementApi;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core;
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
    public async Task Administrator_Gets200_WhenSectionWasGranted()
    {
        await AuthenticateClientAsync(Client, "admin-probe@test.invalid", "1234567890", Constants.Security.AdminGroupKey);

        HttpResponseMessage response = await Client.GetAsync(ProbeUrl);

        // The Administrators group gets the section from the package migration (see AdminGrantMigrationTests).
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
