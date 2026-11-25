using System.Net;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

/// <summary>One test per row of the click table in contracts/public-endpoints.md.</summary>
[TestFixture]
public class ClickEndpointTests : PublicEndpointTestBase
{
    private Task<HttpResponseMessage> ClickAsync(string path, string? userAgent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (userAgent is not null)
        {
            request.Headers.UserAgent.Clear();
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        return Anonymous.SendAsync(request);
    }

    private static void AssertRedirectsToStoredLink(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.ToString(), Is.EqualTo(StoredLink));
        AssertPublicHeaders(response);
    }

    private IReadOnlyList<CounterEntry> Counted() => Buffer.Swap();

    [Test]
    public async Task APublishedBannerInItsZone_Redirects302ToTheStoredLink_AndCountsOneClick()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");

        AssertRedirectsToStoredLink(response);
        Assert.That(Counted(), Is.EqualTo(new[] { new CounterEntry(Today, banner.BannerId, banner.ZoneId, 0, 1) }));
    }

    [Test]
    public async Task AnUnpublishedBanner_StillRedirects_ButCountsNothing()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        Assert.That((await PostAsync($"/banners/{banner.BannerKey}/unpublish")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await WaitForSnapshotAsync(s => s.Zones["home-top"].Candidates.Count == 0);

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");

        AssertRedirectsToStoredLink(response);
        Assert.That(Counted(), Is.Empty);
    }

    [TestCase(null, Description = "missing zone")]
    [TestCase("987654", Description = "unknown zone")]
    public async Task AMissingOrUnknownZone_Redirects_ButCountsNothing(string? zone)
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}" + (zone is null ? string.Empty : "?z=" + zone));

        AssertRedirectsToStoredLink(response);
        Assert.That(Counted(), Is.Empty);
    }

    [Test]
    public async Task AZoneTheBannerIsNotAssignedTo_Redirects_ButCountsNothing()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        (Guid otherZoneKey, _) = await CreateZoneAsync("sidebar");
        int otherZoneId = (await GetRequiredService<Meresoft.Umbraco.Banners.Services.IBannerZoneService>().GetAsync(otherZoneKey))!.Id;

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={otherZoneId}");

        AssertRedirectsToStoredLink(response);
        Assert.That(Counted(), Is.Empty);
    }

    [TestCase("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)")]
    [TestCase("")]
    public async Task ABot_Redirects_ButCountsNothing(string userAgent)
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}", userAgent);

        AssertRedirectsToStoredLink(response);
        Assert.That(Counted(), Is.Empty);
    }

    // Review 3: only a navigation counts. Prefetches, fetch calls and image requests are redirected like any other request.

    private async Task<HttpResponseMessage> ClickWithHeadersAsync(PublishedBanner banner, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");
        foreach ((string name, string value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await Anonymous.SendAsync(request);
    }

    [Test]
    public async Task ANavigationClick_IsCounted()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        AssertRedirectsToStoredLink(await ClickWithHeadersAsync(banner, ("Sec-Fetch-Mode", "navigate"), ("Sec-Fetch-Dest", "document")));

        Assert.That(Counted().Sum(c => c.Clicks), Is.EqualTo(1));
    }

    [Test]
    public async Task AClickWithoutAnyFetchMetadata_IsCounted_ForBrowsersThatSendNone()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        AssertRedirectsToStoredLink(await ClickWithHeadersAsync(banner));

        Assert.That(Counted().Sum(c => c.Clicks), Is.EqualTo(1));
    }

    [TestCase("Sec-Fetch-Mode", "cors")]
    [TestCase("Sec-Fetch-Mode", "no-cors")]
    [TestCase("Sec-Fetch-Mode", "same-origin")]
    [TestCase("Sec-Fetch-Dest", "image")]
    [TestCase("Sec-Fetch-Dest", "empty")]
    [TestCase("Sec-Fetch-Dest", "iframe")]
    [TestCase("Sec-Purpose", "prefetch")]
    [TestCase("Sec-Purpose", "prefetch;prerender")]
    [TestCase("Purpose", "prefetch")]
    [TestCase("Purpose", "Prefetch")]
    public async Task ARequestThatIsNotANavigation_StillRedirects_ButIsNotCounted(string header, string value)
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        AssertRedirectsToStoredLink(await ClickWithHeadersAsync(banner, (header, value)));

        Assert.That(Counted(), Is.Empty);
    }

    [Test]
    public async Task AnUnknownBanner_Is404_AndCountsNothing()
    {
        HttpResponseMessage response = await ClickAsync("/_banners/c/99999?z=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        AssertPublicHeaders(response);
        Assert.That(Counted(), Is.Empty);
    }

    [Test]
    public async Task ABannerThatWasNeverPublished_HasNoLiveVersion_AndIs404()
    {
        (Guid zoneKey, _) = await CreateZoneAsync("home-top");
        Guid bannerKey = await CreateBannerAsync(NewMediaKey(), zoneKey);
        int bannerId = (await GetRequiredService<Meresoft.Umbraco.Banners.Services.IBannerService>().GetAsync(bannerKey))!.Id;

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{bannerId}?z=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(Counted(), Is.Empty);
    }

    [TestCase("abc")]
    [TestCase("12.5")]
    [TestCase("-1")]
    [TestCase("99999999999999")]
    public async Task ANonIntegerBannerId_Is404(string id)
    {
        HttpResponseMessage response = await ClickAsync($"/_banners/c/{id}?z=1");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ANonIntegerZoneId_Is404()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z=abc");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(Counted(), Is.Empty);
    }

    [TestCase("url")]
    [TestCase("returnUrl")]
    [TestCase("to")]
    [TestCase("redirect")]
    public async Task OtherParameters_AreIgnored_ThereIsNoOpenRedirect(string parameter)
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}&{parameter}=https://evil.example/steal");

        AssertRedirectsToStoredLink(response);
    }

    [Test]
    public async Task AnUnknownBannerWithAnEvilParameter_Is404_NotARedirect()
    {
        HttpResponseMessage response = await ClickAsync("/_banners/c/99999?url=https://evil.example");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Headers.Location, Is.Null);
    }

    [Test]
    public async Task AContentNodeWithTheUrlSegmentUnderscoreBanners_DoesNotShadowTheEndpoint()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        CreateContentNodeWithUrlSegment("_banners");

        HttpResponseMessage response = await ClickAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");

        AssertRedirectsToStoredLink(response);
    }

    private void CreateContentNodeWithUrlSegment(string segment)
    {
        var contentTypeService = GetRequiredService<IContentTypeService>();
        var contentService = GetRequiredService<IContentService>();
        var shortStringHelper = GetRequiredService<IShortStringHelper>();

        var type = new ContentType(shortStringHelper, Constants.System.Root)
        {
            Alias = "bannersShadowPage",
            Name = "Shadow page",
            AllowedAsRoot = true,
        };
        type.AddPropertyType(
            new PropertyType(shortStringHelper, Constants.PropertyEditors.Aliases.TextBox, ValueStorageType.Nvarchar, Constants.Conventions.Content.UrlName)
            {
                Name = "Url name",
            });
        contentTypeService.CreateAsync(type, Constants.Security.SuperUserKey).GetAwaiter().GetResult();

        IContent content = contentService.Create("Shadow", Constants.System.Root, type.Alias);
        content.SetValue(Constants.Conventions.Content.UrlName, segment);
        contentService.Save(content);
        contentService.Publish(content, ["*"]);
    }

    [Test]
    public async Task ThePageMarkupLink_PointsAtTheClickAddress_NotTheStoredUrl()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        BannerZoneRenderer renderer = GetRequiredService<BannerZoneRenderer>();

        string html = renderer.Render(new Dictionary<object, object?>(), renderer.Select("home-top"), null, null);

        Assert.That(html, Does.Contain($"href=\"/_banners/c/{banner.BannerId}?z={banner.ZoneId}\""));
        Assert.That(html, Does.Not.Contain(StoredLink));
    }
}
