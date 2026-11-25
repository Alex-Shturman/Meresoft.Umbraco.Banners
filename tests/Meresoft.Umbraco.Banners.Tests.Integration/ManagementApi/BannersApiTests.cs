using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

[TestFixture]
public class BannersApiTests : BannersApiTestBase
{
    private Guid _zoneKey;
    private Guid _media;

    [SetUp]
    public async Task Prepare()
    {
        (_zoneKey, _) = await CreateZoneAsync("home-top");
        _media = NewMediaKey();
    }

    private async Task<JsonElement> GetBannerAsync(Guid key) => await ReadAsync(await GetAsync("/banners/" + key));

    [Test]
    public async Task Create_Returns201WithLocation_AndTheBannerIsADraft()
    {
        HttpResponseMessage response = await PostAsync("/banners", BannerBody(_media, [_zoneKey]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Guid key = KeyFromLocation(response);
        JsonElement banner = await GetBannerAsync(key);
        Assert.Multiple(() =>
        {
            Assert.That(banner.GetProperty("key").GetGuid(), Is.EqualTo(key));
            Assert.That(banner.GetProperty("name").GetString(), Is.EqualTo("Summer promo"));
            Assert.That(banner.GetProperty("type").GetString(), Is.EqualTo("Image"));
            Assert.That(banner.GetProperty("state").GetString(), Is.EqualTo("Draft"));
            Assert.That(banner.GetProperty("live").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(banner.GetProperty("draft").GetProperty("mediaKey").GetGuid(), Is.EqualTo(_media));
            Assert.That(banner.GetProperty("draft").GetProperty("linkUrl").GetString(), Is.EqualTo("https://example.org/show"));
            Assert.That(banner.GetProperty("draft").GetProperty("zoneKeys").EnumerateArray().Select(k => k.GetGuid()), Is.EqualTo(new[] { _zoneKey }));
            Assert.That(banner.GetProperty("flags").GetProperty("notPlaced").GetBoolean(), Is.False);
            Assert.That(banner.TryGetProperty("createDate", out _), Is.True);
            Assert.That(banner.TryGetProperty("updateDate", out _), Is.True);
            Assert.That(banner.TryGetProperty("publishDate", out _), Is.True);
        });
    }

    [Test]
    public async Task Create_HtmlBanner_Returns400_UntilThePermissionCheckExists()
    {
        object body = new { name = "Html offer", type = "html", html = "<p>Invented html</p>", altText = "", weight = 1, openInNewWindow = false, zoneKeys = new[] { _zoneKey } };

        HttpResponseMessage response = await PostAsync("/banners", body);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(response)).GetProperty("errors").EnumerateArray().Any(e => e.TryGetProperty("$.type", out _)), Is.True);
    }

    [TestCase("2.5")]
    [TestCase("null")]
    [TestCase("0")]
    [TestCase("101")]
    public async Task Create_ABadWeight_Returns400WithAFieldErrorOnWeight(string weightJson)
    {
        string json = JsonSerializer.Serialize(BannerBody(_media, [_zoneKey]), Json).Replace("\"weight\":1", "\"weight\":" + weightJson);

        HttpResponseMessage response = await Client.PostAsync(Root + "/banners", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        JsonElement body = await ReadAsync(response);
        Assert.That(body.GetProperty("errors").EnumerateArray().Any(e => e.TryGetProperty("$.weight", out _)), Is.True, body.ToString());
    }

    [Test]
    public async Task Create_InvalidLink_Returns400WithFieldErrors()
    {
        object body = BannerBody(_media, [_zoneKey]);
        string json = JsonSerializer.Serialize(body, Json).Replace("https://example.org/show", "not a url");

        HttpResponseMessage response = await Client.PostAsync(Root + "/banners", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(response)).GetProperty("errors").EnumerateArray().Any(e => e.TryGetProperty("$.linkUrl", out _)), Is.True);
    }

    [Test]
    public async Task Put_SavesTheDraftOnly_AndTheStateShowsUnpublishedChanges()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await Task.Delay(50);
        await PostAsync($"/banners/{key}/publish", new { updateDate = (await GetBannerAsync(key)).GetProperty("updateDate").GetDateTime() });
        await Task.Delay(50);
        JsonElement published = await GetBannerAsync(key);

        HttpResponseMessage response = await PutAsync(
            "/banners/" + key,
            BannerBody(_media, [_zoneKey], published.GetProperty("updateDate").GetDateTime(), altText: "Changed alt text"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        JsonElement banner = await GetBannerAsync(key);
        Assert.Multiple(() =>
        {
            Assert.That(banner.GetProperty("state").GetString(), Is.EqualTo("PublishedWithChanges"));
            Assert.That(banner.GetProperty("draft").GetProperty("altText").GetString(), Is.EqualTo("Changed alt text"));
            Assert.That(banner.GetProperty("live").GetProperty("altText").GetString(), Is.EqualTo("Invented summer promo"));
        });
    }

    [Test]
    public async Task Put_StaleUpdateDate_Returns409()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        DateTime loaded = (await GetBannerAsync(key)).GetProperty("updateDate").GetDateTime();
        await Task.Delay(50);
        await PutAsync("/banners/" + key, BannerBody(_media, [_zoneKey], loaded, altText: "First"));

        HttpResponseMessage response = await PutAsync("/banners/" + key, BannerBody(_media, [_zoneKey], loaded, altText: "Second"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await ReadAsync(response)).GetProperty("type").GetString(), Is.EqualTo("changed-by-another-user"));
    }

    [Test]
    public async Task Put_WithoutUpdateDate_Returns400_SoTheStaleCheckCannotBeSkipped()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        HttpResponseMessage response = await PutAsync("/banners/" + key, BannerBody(_media, [_zoneKey]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(response)).GetProperty("errors").EnumerateArray().Any(e => e.TryGetProperty("$.updateDate", out _)), Is.True);
    }

    [Test]
    public async Task Put_Unknown_Returns404()
    {
        HttpResponseMessage response = await PutAsync("/banners/" + Guid.NewGuid(), BannerBody(_media, [_zoneKey], DateTime.UtcNow));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_Unknown_Returns404()
    {
        Assert.That((await GetAsync("/banners/" + Guid.NewGuid())).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Publish_Returns200_AndTheStateIsPublished()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        HttpResponseMessage response = await PublishAsync(key);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        JsonElement banner = await GetBannerAsync(key);
        Assert.That(banner.GetProperty("state").GetString(), Is.EqualTo("Published"));
        Assert.That(banner.GetProperty("publishDate").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        Assert.That(banner.GetProperty("live").GetProperty("altText").GetString(), Is.EqualTo("Invented summer promo"));
    }

    [Test]
    public async Task Publish_WithoutAZone_Returns400()
    {
        Guid key = await CreateBannerAsync(_media);

        HttpResponseMessage response = await PublishAsync(key);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(response)).GetProperty("type").GetString(), Is.EqualTo("no-zone-assigned"));
    }

    [Test]
    public async Task Publish_WithoutUpdateDate_Returns400_SoTheStaleCheckCannotBeSkipped()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        HttpResponseMessage response = await PostAsync($"/banners/{key}/publish", new { updateDate = (DateTime?)null });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(response)).GetProperty("errors").EnumerateArray().Any(e => e.TryGetProperty("$.updateDate", out _)), Is.True);
        Assert.That((await GetBannerAsync(key)).GetProperty("state").GetString(), Is.EqualTo("Draft"));
    }

    [Test]
    public async Task Publish_Unknown_Returns404()
    {
        Assert.That((await PostAsync($"/banners/{Guid.NewGuid()}/publish", new { updateDate = DateTime.UtcNow })).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Publish_StaleUpdateDate_Returns409()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        HttpResponseMessage response = await PostAsync($"/banners/{key}/publish", new { updateDate = DateTime.UtcNow.AddDays(-1) });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Unpublish_Returns200_AndTheStateIsUnpublished()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await PublishAsync(key);

        HttpResponseMessage response = await PostAsync($"/banners/{key}/unpublish");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await GetBannerAsync(key)).GetProperty("state").GetString(), Is.EqualTo("Unpublished"));
    }

    [Test]
    public async Task Unpublish_Unknown_Returns404()
    {
        Assert.That((await PostAsync($"/banners/{Guid.NewGuid()}/unpublish")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Copy_Returns201WithLocation_AndTheCopyIsADraft()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        HttpResponseMessage response = await PostAsync($"/banners/{key}/copy", new { name = "Copy of promo" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        JsonElement copy = await GetBannerAsync(KeyFromLocation(response));
        Assert.That(copy.GetProperty("name").GetString(), Is.EqualTo("Copy of promo"));
        Assert.That(copy.GetProperty("state").GetString(), Is.EqualTo("Draft"));
        Assert.That(copy.GetProperty("draft").GetProperty("zoneKeys").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Copy_Unknown_Returns404()
    {
        Assert.That((await PostAsync($"/banners/{Guid.NewGuid()}/copy", new { name = (string?)null })).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Returns204_ThenGetIs404()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        Assert.That((await DeleteAsync("/banners/" + key)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await GetAsync("/banners/" + key)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Unknown_Returns404()
    {
        Assert.That((await DeleteAsync("/banners/" + Guid.NewGuid())).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task List_ReturnsAPage_WithFiltersAndPaging()
    {
        (Guid otherZone, _) = await CreateZoneAsync("sidebar");
        Guid a = await CreateBannerAsync(_media, _zoneKey);
        Guid b = await CreateBannerAsync(_media, otherZone);
        Guid c = await CreateBannerAsync(_media, _zoneKey);
        await PublishAsync(c);

        JsonElement all = await ReadAsync(await GetAsync("/banners"));
        JsonElement inSidebar = await ReadAsync(await GetAsync($"/banners?zone={otherZone}"));
        JsonElement published = await ReadAsync(await GetAsync("/banners?state=published"));
        JsonElement drafts = await ReadAsync(await GetAsync("/banners?state=draft"));
        JsonElement html = await ReadAsync(await GetAsync("/banners?type=html"));
        JsonElement paged = await ReadAsync(await GetAsync("/banners?skip=0&take=2"));

        Assert.Multiple(() =>
        {
            Assert.That(all.GetProperty("total").GetInt32(), Is.EqualTo(3));
            Assert.That(inSidebar.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetGuid()), Is.EqualTo(new[] { b }));
            Assert.That(published.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetGuid()), Is.EqualTo(new[] { c }));
            Assert.That(drafts.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetGuid()), Is.EquivalentTo(new[] { a, b }));
            Assert.That(html.GetProperty("total").GetInt32(), Is.EqualTo(0));
            Assert.That(paged.GetProperty("items").GetArrayLength(), Is.EqualTo(2));
            Assert.That(paged.GetProperty("total").GetInt32(), Is.EqualTo(3));
        });
        JsonElement item = all.GetProperty("items").EnumerateArray().First(i => i.GetProperty("key").GetGuid() == c);
        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("name").GetString(), Is.EqualTo("Summer promo"));
            Assert.That(item.GetProperty("type").GetString(), Is.EqualTo("Image"));
            Assert.That(item.GetProperty("state").GetString(), Is.EqualTo("Published"));
            Assert.That(item.GetProperty("flags").GetProperty("notPlaced").GetBoolean(), Is.False);
            Assert.That(item.GetProperty("zoneKeys").EnumerateArray().Select(k => k.GetGuid()), Is.EqualTo(new[] { _zoneKey }));
            Assert.That(item.GetProperty("weight").GetInt32(), Is.EqualTo(1));
            Assert.That(item.TryGetProperty("updateDate", out _), Is.True);
        });
    }

    [Test]
    public async Task List_TakeOver100_Returns400()
    {
        Assert.That((await GetAsync("/banners?take=101")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await GetAsync("/banners?take=100")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ZoneList_CountsTheActiveBanners_AfterPublish()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await PublishAsync(key);

        JsonElement zone = default;
        DateTime stop = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < stop)
        {
            zone = (await ReadAsync(await GetAsync("/zones"))).EnumerateArray().First();
            if (zone.GetProperty("activeBannerCount").GetInt32() == 1)
            {
                break;
            }

            await Task.Delay(100);
        }

        Assert.That(zone.GetProperty("activeBannerCount").GetInt32(), Is.EqualTo(1));
    }
}
