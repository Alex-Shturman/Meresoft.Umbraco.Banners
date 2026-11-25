using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

/// <summary>US6 scenario 1: a user whose groups lack the Banners section is refused by every package endpoint.</summary>
[TestFixture]
public class SectionPermissionTests : BannersApiTestBase
{
    private const string DocumentUrl = "/umbraco/swagger/meresoft-banners/swagger.json";
    private const string PackageSegment = "/meresoft-banners/";

    /// <summary>Reads the operations from the package's OpenAPI document, so a new endpoint is covered without editing this test.</summary>
    private async Task<IReadOnlyList<(HttpMethod Method, string Url)>> PackageOperationsAsync()
    {
        using JsonDocument document = JsonDocument.Parse(await Client.GetStringAsync(DocumentUrl));
        var operations = new List<(HttpMethod, string)>();
        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            string relative = path.Name[path.Name.IndexOf(PackageSegment, StringComparison.Ordinal)..];
            string url = "/umbraco/management/api/v1" + FillParameters(relative);
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                operations.Add((new HttpMethod(operation.Name.ToUpperInvariant()), url));
            }
        }

        return operations;
    }

    private static string FillParameters(string template) =>
        Regex.Replace(template, "\\{(?<name>[^}]+)\\}", match =>
            match.Groups["name"].Value.Contains("alias", StringComparison.OrdinalIgnoreCase) ? "some-alias" : Guid.NewGuid().ToString());

    [Test]
    public async Task TheDocumentListsTheOperations_ThatTheEnumerationCovers()
    {
        IReadOnlyList<(HttpMethod Method, string Url)> operations = await PackageOperationsAsync();

        Assert.That(operations.Count, Is.GreaterThan(15), "zones, banners and requested zones are in the document");
    }

    [Test]
    public async Task EveryPackageEndpoint_Returns403_WithoutTheBannersSection()
    {
        IReadOnlyList<(HttpMethod Method, string Url)> operations = await PackageOperationsAsync();
        await SignInWithGroupsAsync("no-section@test.invalid", (Section: false, Html: true));

        var notForbidden = new List<string>();
        foreach ((HttpMethod method, string url) in operations)
        {
            using var request = new HttpRequestMessage(method, url);
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                request.Content = JsonContent.Create(new { }, options: Json);
            }

            HttpResponseMessage response = await Client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                notForbidden.Add($"{method} {url} -> {(int)response.StatusCode}");
            }
        }

        Assert.That(notForbidden, Is.Empty);
    }

    [Test]
    public async Task WithTheBannersSection_TheSameEndpointsAreReachable()
    {
        await SignInWithGroupsAsync("with-section@test.invalid", (Section: true, Html: false));

        HttpResponseMessage zones = await GetAsync("/zones");

        Assert.That(zones.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
