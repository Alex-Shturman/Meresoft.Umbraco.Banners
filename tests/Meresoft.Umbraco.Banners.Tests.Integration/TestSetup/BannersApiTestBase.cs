using System.Net.Http.Json;
using NUnit.Framework;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Integration.ManagementApi;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Base of the management API tests: an administrator (who owns the Banners section through the
/// package migration) is signed in, and small helpers send and read JSON.
/// </summary>
public abstract class BannersApiTestBase : ManagementApiTest<SectionProbeController>
{
    protected const string Root = "/umbraco/management/api/v1/meresoft-banners";

    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override System.Linq.Expressions.Expression<Func<SectionProbeController, object>> MethodSelector
    {
        get => x => x.Probe();
        set { }
    }

    protected override void CustomMvcSetup(IMvcBuilder mvcBuilder) =>
        mvcBuilder
            .AddApplicationPart(typeof(SectionProbeController).Assembly)
            .AddApplicationPart(typeof(BannersManagementApiControllerBase).Assembly);

    protected override void CustomTestSetup(IUmbracoBuilder builder) =>
        builder.Services.AddSingleton<IServerMessenger, LocalServerMessenger>();

    [SetUp]
    public async Task SignIn() =>
        await AuthenticateClientAsync(Client, "banners-admin@test.invalid", "1234567890", Constants.Security.AdminGroupKey);

    /// <summary>The database is shared by the tests of a fixture, so each test removes what it created.</summary>
    [TearDown]
    public async Task RemoveCreatedData()
    {
        IBannerService banners = GetRequiredService<IBannerService>();
        IBannerZoneService zones = GetRequiredService<IBannerZoneService>();
        foreach (BannerListItem banner in (await banners.GetPagedAsync(new BannerQuery { Take = BannerQuery.MaxTake })).Items)
        {
            await banners.DeleteAsync(banner.Key, Constants.Security.SuperUserKey);
        }

        foreach (BannerZone zone in await zones.GetAllAsync())
        {
            await zones.DeleteAsync(zone.Key, Constants.Security.SuperUserKey);
        }

        foreach (RequestedZone requested in await zones.GetRequestedZonesAsync())
        {
            await zones.DismissRequestedZoneAsync(requested.Alias);
        }
    }

    protected IScopeProvider ScopeProvider => GetRequiredService<IScopeProvider>();

    protected Task<HttpResponseMessage> PostAsync(string path, object? body = null) =>
        Client.PostAsJsonAsync(Root + path, body ?? new { }, Json);

    protected Task<HttpResponseMessage> PutAsync(string path, object body) =>
        Client.PutAsJsonAsync(Root + path, body, Json);

    protected Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(Root + path);

    protected Task<HttpResponseMessage> DeleteAsync(string path) => Client.DeleteAsync(Root + path);

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>The key at the end of the Location header of a 201 response.</summary>
    protected static Guid KeyFromLocation(HttpResponseMessage response) =>
        Guid.Parse(response.Headers.Location!.ToString().TrimEnd('/').Split('/').Last().Split('?')[0]);

    protected Guid NewMediaKey() => TestMedia.CreateImage(GetRequiredService<IMediaService>()).Key;

    protected static object ZoneBody(string alias, object? updateDate = null, int showCount = 1, string renderMode = "server") => new
    {
        alias,
        name = "Zone " + alias,
        description = "Invented zone",
        showCount,
        imageWidth = 728,
        imageHeight = 90,
        renderMode,
        updateDate,
    };

    protected async Task<(Guid Key, JsonElement Body)> CreateZoneAsync(string alias)
    {
        HttpResponseMessage response = await PostAsync("/zones", ZoneBody(alias));
        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        Guid key = KeyFromLocation(response);
        return (key, await ReadAsync(await GetAsync("/zones/" + key)));
    }

    protected object BannerBody(Guid mediaKey, Guid[] zoneKeys, object? updateDate = null, string name = "Summer promo", string altText = "Invented summer promo") => new
    {
        name,
        type = "image",
        mediaKey,
        linkUrl = "https://example.org/show",
        openInNewWindow = false,
        altText,
        title = (string?)null,
        weight = 1,
        zoneKeys,
        updateDate,
    };

    protected async Task<Guid> CreateBannerAsync(Guid mediaKey, params Guid[] zoneKeys)
    {
        HttpResponseMessage response = await PostAsync("/banners", BannerBody(mediaKey, zoneKeys));
        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        return KeyFromLocation(response);
    }
}
