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

    private async Task<JsonDocument> ReadDocumentAsync()
    {
        HttpResponseMessage response = await Client.GetAsync(DocumentUrl);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
