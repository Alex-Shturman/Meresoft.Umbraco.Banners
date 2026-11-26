using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

/// <summary>One test per row of the client mode delivery endpoint in contracts/public-endpoints.md.</summary>
[TestFixture]
public class ZoneDeliveryEndpointTests : PublicEndpointTestBase
{
    private const string StoredHtml = "<p>Invented delivery html</p>";

    private async Task<(HttpResponseMessage Response, JsonElement Body)> GetZoneAsync(string alias)
    {
        HttpResponseMessage response = await Anonymous.GetAsync($"/_banners/zone/{alias}");
        string text = await response.Content.ReadAsStringAsync();
        return (response, response.StatusCode == HttpStatusCode.OK ? JsonDocument.Parse(text).RootElement : default);
    }

    /// <summary>Creates a published Html banner in an existing zone.</summary>
    private async Task<int> PublishHtmlBannerInAsync(Guid zoneKey, string alias)
    {
        var draft = new BannerDraftModel { Name = "Secret html name", Html = StoredHtml, Weight = 7, ZoneKeys = [zoneKey] };
        var created = await GetRequiredService<IBannerService>().CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = draft }, Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True, created.Status.ToString());
        HttpResponseMessage published = await PublishAsync(created.Result!.Key);
        Assert.That(published.StatusCode, Is.EqualTo(HttpStatusCode.OK), await published.Content.ReadAsStringAsync());
        int id = created.Result.Id;
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue(alias, out var z) && z.Candidates.Any(c => c.BannerId == id));
        return id;
    }

    private async Task ShowTwoAtOnceAsync(Guid zoneKey, string alias)
    {
        BannerZone zone = (await GetRequiredService<IBannerZoneService>().GetAsync(zoneKey))!;
        HttpResponseMessage response = await PutAsync($"/zones/{zoneKey}", ZoneBody(alias, zone.UpdateDate, showCount: 2));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        await WaitForSnapshotAsync(s => s.Zones[alias].ShowCount == 2);
    }

    [Test]
    public async Task AnImageBanner_IsReturnedWithPublicFieldsOnly_AndTheHeadersOfTheOtherPublicEndpoints()
    {
        PublishedBanner banner = await PublishBannerAsync("deliver-image");

        (HttpResponseMessage response, JsonElement body) = await GetZoneAsync("deliver-image");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        AssertPublicHeaders(response);
        Assert.That(body.GetProperty("zone").GetString(), Is.EqualTo("deliver-image"));
        Assert.That(body.GetProperty("token").GetString(), Is.Not.Empty);
        JsonElement item = body.GetProperty("banners").EnumerateArray().Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("id").GetInt32(), Is.EqualTo(banner.BannerId));
            Assert.That(item.GetProperty("type").GetString(), Is.EqualTo("image"));
            Assert.That(item.GetProperty("clickUrl").GetString(), Is.EqualTo($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}"));
            Assert.That(item.GetProperty("newWindow").GetBoolean(), Is.False);
            Assert.That(item.GetProperty("imageUrl").GetString(), Does.Contain("/media/"));
            Assert.That(item.GetProperty("width").GetInt32(), Is.EqualTo(728));
            Assert.That(item.GetProperty("height").GetInt32(), Is.EqualTo(90));
            Assert.That(item.GetProperty("alt").GetString(), Is.EqualTo("Invented summer promo"));
            Assert.That(item.TryGetProperty("html", out _), Is.False);
            string[] allowed = ["id", "type", "clickUrl", "newWindow", "imageUrl", "width", "height", "alt", "title"];
            Assert.That(item.EnumerateObject().Select(p => p.Name), Is.SubsetOf(allowed), "only public fields");
        });
    }

    [Test]
    public async Task AnHtmlBanner_IsReturnedWithIdTypeAndHtmlOnly()
    {
        PublishedBanner image = await PublishBannerAsync("deliver-mixed");
        int htmlId = await PublishHtmlBannerInAsync(image.ZoneKey, "deliver-mixed");
        await ShowTwoAtOnceAsync(image.ZoneKey, "deliver-mixed");

        (_, JsonElement body) = await GetZoneAsync("deliver-mixed");

        JsonElement html = body.GetProperty("banners").EnumerateArray().Single(b => b.GetProperty("id").GetInt32() == htmlId);
        Assert.Multiple(() =>
        {
            Assert.That(html.GetProperty("type").GetString(), Is.EqualTo("html"));
            Assert.That(html.GetProperty("html").GetString(), Is.EqualTo(StoredHtml));
            Assert.That(html.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "type", "html" }));
        });
        string raw = body.GetRawText();
        Assert.That(raw, Does.Not.Contain("Secret html name").And.Not.Contain("weight").And.Not.Contain("schedule").And.Not.Contain("draft"));
    }

    [Test]
    public async Task TheReturnedToken_IsAcceptedBySeen_ForTheZoneAndTheReturnedBanner()
    {
        PublishedBanner banner = await PublishBannerAsync("deliver-seen");
        (_, JsonElement body) = await GetZoneAsync("deliver-seen");
        string token = body.GetProperty("token").GetString()!;

        HttpResponseMessage seen = await Anonymous.PostAsync("/_banners/seen", PlainText($"{token} {banner.BannerId}"));

        Assert.That(seen.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(Buffer.Swap(), Is.EqualTo(new[] { new CounterEntry(Today, banner.BannerId, banner.ZoneId, 1, 0) }));
    }

    [Test]
    public async Task AnUnknownAlias_Gives200WithNoBannersAndNoToken_AndIsNotRecordedAsRequested()
    {
        (HttpResponseMessage response, JsonElement body) = await GetZoneAsync("never-created-zone");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertPublicHeaders(response);
        Assert.That(body.GetProperty("banners").GetArrayLength(), Is.Zero);
        Assert.That(body.TryGetProperty("token", out JsonElement token) ? token.ValueKind : JsonValueKind.Null, Is.EqualTo(JsonValueKind.Null));
        // Only the tag helper records missing zones. An anonymous caller must not be able to fill the cap with junk.
        Assert.That(GetRequiredService<RequestedZoneRecorder>().Snapshot().Select(z => z.Alias), Does.Not.Contain("never-created-zone"));
    }

    [Test]
    public async Task AZoneWithNoActiveBanner_Gives200WithNoBannersAndNoToken()
    {
        await CreateZoneAsync("deliver-empty");

        (HttpResponseMessage response, JsonElement body) = await GetZoneAsync("deliver-empty");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body.GetProperty("banners").GetArrayLength(), Is.Zero);
        Assert.That(body.TryGetProperty("token", out JsonElement token) ? token.ValueKind : JsonValueKind.Null, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ABannerThatReachedItsImpressionLimit_IsNotDelivered_LikeInTheServerModeSnapshot()
    {
        (Guid zoneKey, _) = await CreateZoneAsync("deliver-limit");
        var draft = new BannerDraftModel
        {
            Name = "Limited promo",
            MediaKey = NewMediaKey(),
            LinkUrl = "https://example.org/show",
            AltText = "Invented limited promo",
            Weight = 1,
            ImpressionLimit = 1,
            ZoneKeys = [zoneKey],
        };
        var created = await GetRequiredService<IBannerService>().CreateAsync(new BannerCreateModel { Type = BannerType.Image, Draft = draft }, Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True, created.Status.ToString());
        Assert.That((await PublishAsync(created.Result!.Key)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        int bannerId = created.Result.Id;
        int zoneId = (await GetRequiredService<IBannerZoneService>().GetAsync(zoneKey))!.Id;
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue("deliver-limit", out var z) && z.Candidates.Any(c => c.BannerId == bannerId));
        (_, JsonElement before) = await GetZoneAsync("deliver-limit");
        Assert.That(before.GetProperty("banners").GetArrayLength(), Is.EqualTo(1), "delivered while below the limit");

        Buffer.AddImpression(Today, bannerId, zoneId);
        await ActivatorUtilities.CreateInstance<BannersFlushJob>(Factory.Services).RunJobAsync(CancellationToken.None);

        (_, JsonElement after) = await GetZoneAsync("deliver-limit");
        Assert.That(after.GetProperty("banners").GetArrayLength(), Is.Zero);
        Assert.That(after.TryGetProperty("token", out JsonElement token) ? token.ValueKind : JsonValueKind.Null, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task TheAlias_IsNormalised()
    {
        await PublishBannerAsync("deliver-case");

        (HttpResponseMessage response, JsonElement body) = await GetZoneAsync("%20Deliver-CASE%20");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body.GetProperty("zone").GetString(), Is.EqualTo("deliver-case"));
        Assert.That(body.GetProperty("banners").GetArrayLength(), Is.EqualTo(1));
    }

    [TestCase("9starts-with-digit")]
    [TestCase("has.dot")]
    [TestCase("a%20b")]
    public async Task AnAliasThatFailsTheAliasRules_Gives404(string alias)
    {
        HttpResponseMessage response = await Anonymous.GetAsync($"/_banners/zone/{alias}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        AssertPublicHeaders(response);
    }

    /// <summary>The same endpoint with <c>DeliveryEndpointEnabled=false</c>.</summary>
    [TestFixture]
    public class WhenDisabled : PublicEndpointTestBase
    {
        protected override void CustomTestSetup(IUmbracoBuilder builder)
        {
            base.CustomTestSetup(builder);
            builder.Services.Configure<BannersOptions>(o => o.DeliveryEndpointEnabled = false);
        }

        [Test]
        public async Task Gives404_ForAnExistingZone_AndAnUnknownOne()
        {
            await PublishBannerAsync("deliver-off");

            HttpResponseMessage existing = await Anonymous.GetAsync("/_banners/zone/deliver-off");
            HttpResponseMessage unknown = await Anonymous.GetAsync("/_banners/zone/nothing-here");

            Assert.That(existing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            AssertPublicHeaders(existing);
        }
    }
}
