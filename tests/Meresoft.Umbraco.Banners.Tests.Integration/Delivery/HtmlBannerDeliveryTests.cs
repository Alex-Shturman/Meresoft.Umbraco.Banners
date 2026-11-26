using System.Net;
using System.Text.RegularExpressions;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

/// <summary>
/// Html banners next to the public endpoints (FR-006, contracts/markup.md, contracts/public-endpoints.md): no click
/// address exists for them, and their impressions count through the zone's view token like any other banner.
/// </summary>
[TestFixture]
public class HtmlBannerDeliveryTests : PublicEndpointTestBase
{
    private const string StoredHtml = "<p>Invented html offer</p>";

    /// <summary>Creates the zone and a published Html banner (created by the super user, whose group holds the verb).</summary>
    private async Task<PublishedBanner> PublishHtmlBannerAsync(string zoneAlias, string? linkUrl = null)
    {
        (Guid zoneKey, _) = await CreateZoneAsync(zoneAlias);
        var draft = new BannerDraftModel { Name = "Html offer", Html = StoredHtml, LinkUrl = linkUrl, Weight = 1, ZoneKeys = [zoneKey] };
        var created = await GetRequiredService<IBannerService>().CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = draft }, Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True, created.Status.ToString());
        HttpResponseMessage published = await PublishAsync(created.Result!.Key);
        Assert.That(published.StatusCode, Is.EqualTo(HttpStatusCode.OK), await published.Content.ReadAsStringAsync());

        BannerZone zone = (await GetRequiredService<IBannerZoneService>().GetAsync(zoneKey))!;
        int bannerId = created.Result.Id;
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue(zoneAlias, out var z) && z.Candidates.Any(c => c.BannerId == bannerId));
        return new PublishedBanner(bannerId, created.Result.Key, zone.Id, zoneKey);
    }

    [TestCase(null)]
    [TestCase("https://example.org/ignored")]
    public async Task AnHtmlBanner_HasNoClickAddress_Gives404_AndCountsNothing(string? linkUrl)
    {
        PublishedBanner banner = await PublishHtmlBannerAsync("html-click", linkUrl);

        HttpResponseMessage withZone = await Anonymous.GetAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");
        HttpResponseMessage withoutZone = await Anonymous.GetAsync($"/_banners/c/{banner.BannerId}");

        Assert.Multiple(() =>
        {
            Assert.That(withZone.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(withoutZone.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(Buffer.Swap(), Is.Empty);
        });
    }

    [Test]
    public async Task AnUnpublishedHtmlBanner_StillGives404_FromTheDatabasePath()
    {
        PublishedBanner banner = await PublishHtmlBannerAsync("html-click-2");
        await PostAsync($"/banners/{banner.BannerKey}/unpublish");
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue("html-click-2", out var z) && z.Candidates.All(c => c.BannerId != banner.BannerId));

        HttpResponseMessage response = await Anonymous.GetAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task TheRenderedHtmlBanner_HasNoClickLink_AndItsImpressionCountsThroughTheZoneToken()
    {
        PublishedBanner banner = await PublishHtmlBannerAsync("html-seen");
        BannerZoneRenderer renderer = GetRequiredService<BannerZoneRenderer>();

        string html = renderer.Render(new Dictionary<object, object?>(), renderer.Select("html-seen"), null, null);

        Assert.That(html, Does.Contain($"<div class=\"mb-banner mb-html\" data-mbb=\"{banner.BannerId}\">{StoredHtml}</div>"));
        Assert.That(html, Does.Not.Contain("/_banners/c/"), "no click address for foreign code");
        string token = Regex.Match(html, "data-mbz-token=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty);

        HttpResponseMessage seen = await Anonymous.PostAsync("/_banners/seen", PlainText($"{token} {banner.BannerId}"));

        Assert.That(seen.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(Buffer.Swap(), Is.EqualTo(new[] { new CounterEntry(Today, banner.BannerId, banner.ZoneId, 1, 0) }));
    }
}
