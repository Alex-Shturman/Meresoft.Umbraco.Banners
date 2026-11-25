using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.TestServerTest;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerFixture)]
public class OpenApiDocumentTests : UmbracoTestServerTestBase
{
    private const string DocumentUrl = "/umbraco/swagger/meresoft-banners/swagger.json";

    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder) =>
        mvcBuilder.AddApplicationPart(typeof(SectionProbeController).Assembly);

    [Test]
    public async Task Document_IsServed()
    {
        HttpResponseMessage response = await Client.GetAsync(DocumentUrl);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Document_ContainsOnlyPackageOperations()
    {
        using JsonDocument document = await ReadDocumentAsync();

        string[] paths = document.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToArray();

        Assert.That(paths, Has.Some.Contain("/meresoft-banners/section-probe"), "the test probe controller is part of the package document");
        Assert.That(paths, Has.All.Contain("/meresoft-banners/"), "only package operations belong in this document");
    }

    [Test]
    public async Task Document_DeclaresBackOfficeBearerSecurity()
    {
        using JsonDocument document = await ReadDocumentAsync();

        JsonElement schemes = document.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.That(schemes.EnumerateObject().Select(s => s.Name), Does.Contain("Backoffice-User"));
        Assert.That(document.RootElement.GetProperty("info").GetProperty("title").GetString(), Does.Contain("Banners"));
    }

    [Test]
    public async Task Operations_HaveShortExplicitIds()
    {
        using JsonDocument document = await ReadDocumentAsync();

        string?[] ids = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(path => !path.Name.Contains("section-probe"))
            .SelectMany(path => path.Value.EnumerateObject())
            .Select(operation => operation.Value.TryGetProperty("operationId", out JsonElement id) ? id.GetString() : null)
            .ToArray();

        Assert.That(
            ids,
            Is.EquivalentTo(new[]
            {
                "listZones", "getZone", "createZone", "updateZone", "deleteZone",
                "listRequestedZones", "dismissRequestedZone",
                "listBanners", "getBanner", "createBanner", "updateBanner", "publishBanner", "unpublishBanner", "copyBanner", "deleteBanner",
                "getReport", "getReportCsv",
            }));
    }

    [Test]
    public async Task BannerRequestWeight_IsANullableNumber_WithItsRangeDescribed()
    {
        using JsonDocument document = await ReadDocumentAsync();

        JsonElement weight = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("BannerRequestModel").GetProperty("properties").GetProperty("weight");

        Assert.Multiple(() =>
        {
            Assert.That(weight.GetProperty("type").GetString(), Is.EqualTo("number"));
            Assert.That(weight.TryGetProperty("nullable", out JsonElement nullable) && nullable.GetBoolean(), Is.True, weight.ToString());
            Assert.That(
                weight.TryGetProperty("minimum", out JsonElement min) && min.GetDouble() == 1
                && weight.TryGetProperty("maximum", out JsonElement max) && max.GetDouble() == 100,
                Is.True,
                "the 1 to 100 range is part of the schema: " + weight);
        });
    }

    private async Task<JsonDocument> ReadDocumentAsync()
    {
        HttpResponseMessage response = await Client.GetAsync(DocumentUrl);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
