using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

/// <summary>US6 scenario 2 and FR-006: the HTML permission guards creating and changing Html banners, nothing else.</summary>
[TestFixture]
public class HtmlPermissionTests : BannersApiTestBase
{
    private const string StoredHtml = "<p>Invented html</p>";

    private Guid _zoneKey;

    [SetUp]
    public async Task Prepare() => (_zoneKey, _) = await CreateZoneAsync("html-zone");

    private object HtmlBody(string html = StoredHtml, string name = "Html offer", object? updateDate = null) => new
    {
        name,
        type = "html",
        html,
        altText = "",
        weight = 1,
        openInNewWindow = false,
        zoneKeys = new[] { _zoneKey },
        updateDate,
    };

    /// <summary>Creates an Html banner as the signed in administrator, who holds the verb.</summary>
    private async Task<Guid> HtmlBannerCreatedByAdminAsync()
    {
        HttpResponseMessage response = await PostAsync("/banners", HtmlBody());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        return KeyFromLocation(response);
    }

    private async Task<object> BodyWithCurrentDateAsync(Guid key, string html = StoredHtml, string name = "Html offer")
    {
        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));
        return HtmlBody(html, name, banner.GetProperty("updateDate").GetDateTime());
    }

    private static async Task AssertHtmlPermissionRequiredAsync(HttpResponseMessage response)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        JsonElement problem = await ReadAsync(response);
        Assert.That(problem.GetProperty("type").GetString(), Does.EndWith("html-permission-required"));
    }

    [Test]
    public async Task WithoutTheVerb_CreatingAnHtmlBanner_Gives403HtmlPermissionRequired()
    {
        await SignInWithGroupsAsync("no-html-create@test.invalid", (Section: true, Html: false));

        await AssertHtmlPermissionRequiredAsync(await PostAsync("/banners", HtmlBody()));

        Assert.That((await ReadAsync(await GetAsync("/banners?type=html"))).GetProperty("total").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task WithoutTheVerb_ChangingTheHtml_Gives403_AndTheStoredHtmlStays()
    {
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-change@test.invalid", (Section: true, Html: false));

        await AssertHtmlPermissionRequiredAsync(await PutAsync("/banners/" + key, await BodyWithCurrentDateAsync(key, "<p>Changed</p>")));

        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));
        Assert.That(banner.GetProperty("draft").GetProperty("html").GetString(), Is.EqualTo(StoredHtml));
    }

    [Test]
    public async Task WithoutTheVerb_SavingOtherFields_WithTheHtmlUnchanged_Succeeds()
    {
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-save@test.invalid", (Section: true, Html: false));

        HttpResponseMessage response = await PutAsync("/banners/" + key, await BodyWithCurrentDateAsync(key, name: "Renamed html offer"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That((await ReadAsync(await GetAsync("/banners/" + key))).GetProperty("name").GetString(), Is.EqualTo("Renamed html offer"));
    }

    [Test]
    public async Task WithoutTheVerb_PublishUnpublishCopyAndDelete_Succeed()
    {
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-lifecycle@test.invalid", (Section: true, Html: false));

        HttpResponseMessage publish = await PublishAsync(key);
        HttpResponseMessage unpublish = await PostAsync($"/banners/{key}/unpublish");
        HttpResponseMessage copy = await PostAsync($"/banners/{key}/copy");
        HttpResponseMessage deleteCopy = await DeleteAsync("/banners/" + KeyFromLocation(copy));
        HttpResponseMessage delete = await DeleteAsync("/banners/" + key);

        Assert.Multiple(() =>
        {
            Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(unpublish.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(copy.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(deleteCopy.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        });
    }

    [Test]
    public async Task WithoutTheVerb_ReportsOfAnHtmlBanner_AreAvailable()
    {
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-reports@test.invalid", (Section: true, Html: false));
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        HttpResponseMessage response = await GetAsync($"/reports?from={today}&to={today}&banner={key}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task WithTheVerbInOneOfSeveralGroups_CreateAndChangeSucceed()
    {
        await SignInWithGroupsAsync("html-in-second-group@test.invalid", (Section: true, Html: false), (Section: false, Html: true));

        HttpResponseMessage create = await PostAsync("/banners", HtmlBody());
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), await create.Content.ReadAsStringAsync());
        Guid key = KeyFromLocation(create);
        HttpResponseMessage change = await PutAsync("/banners/" + key, await BodyWithCurrentDateAsync(key, "<p>Changed</p>"));

        Assert.That(change.StatusCode, Is.EqualTo(HttpStatusCode.OK), await change.Content.ReadAsStringAsync());
        Assert.That((await ReadAsync(await GetAsync("/banners/" + key))).GetProperty("draft").GetProperty("html").GetString(), Is.EqualTo("<p>Changed</p>"));
    }

    [Test]
    public async Task ImageBanners_NeedNoHtmlVerb()
    {
        await SignInWithGroupsAsync("no-html-image@test.invalid", (Section: true, Html: false));

        HttpResponseMessage response = await PostAsync("/banners", BannerBody(NewMediaKey(), [_zoneKey]));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task WithoutTheVerb_AnImageBanner_CannotStoreHtmlText_ThroughUpdate()
    {
        // The html column exists on every banner row, so the check covers any change of the value, not only Html banners.
        Guid key = await CreateBannerAsync(NewMediaKey(), _zoneKey);
        await SignInWithGroupsAsync("no-html-image-update@test.invalid", (Section: true, Html: false));
        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));
        string json = JsonSerializer.Serialize(BannerBody(NewMediaKey(), [_zoneKey], banner.GetProperty("updateDate").GetDateTime()), Json)
            .Replace("\"weight\":1", "\"weight\":1,\"html\":\"<script>alert(1)</script>\"");

        HttpResponseMessage response = await Client.PutAsync(Root + "/banners/" + key, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        await AssertHtmlPermissionRequiredAsync(response);
    }

    [Test]
    public async Task WithoutTheVerb_ChangingAnImageBannerToHtml_Gives403()
    {
        Guid key = await CreateBannerAsync(NewMediaKey(), _zoneKey);
        await SignInWithGroupsAsync("no-html-type-change@test.invalid", (Section: true, Html: false));
        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));

        HttpResponseMessage response = await PutAsync("/banners/" + key, HtmlBody(updateDate: banner.GetProperty("updateDate").GetDateTime()));

        await AssertHtmlPermissionRequiredAsync(response);
    }

    [Test]
    public async Task WithTheVerb_ChangingAnImageBannerToHtml_IsStillRefused_BecauseTheTypeIsFixed()
    {
        Guid key = await CreateBannerAsync(NewMediaKey(), _zoneKey);
        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));

        HttpResponseMessage response = await PutAsync("/banners/" + key, HtmlBody(updateDate: banner.GetProperty("updateDate").GetDateTime()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task WithoutTheVerb_CopyingAnHtmlBanner_KeepsTheStoredHtml_AndNeedsNoVerb()
    {
        // A copy only repeats what an authorized editor already stored; nobody can write new markup through it.
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-copy@test.invalid", (Section: true, Html: false));

        Guid copyKey = KeyFromLocation(await PostAsync($"/banners/{key}/copy"));

        JsonElement copy = await ReadAsync(await GetAsync("/banners/" + copyKey));
        Assert.That(copy.GetProperty("draft").GetProperty("html").GetString(), Is.EqualTo(StoredHtml));
    }

    [Test]
    public async Task APasswordlessCaller_Gets401_OnCreate()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await PostAsync("/banners", HtmlBody());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    /// <summary>The key of the user the helper signed in.</summary>
    private Guid UserKeyOf(string email) => GetRequiredService<IUserService>().GetByEmail(email)!.Key;

    [Test]
    public async Task WithoutTheVerb_UpdatingTheHtml_OfABannerWithNoDraft_Gives403()
    {
        // A banner that only has a live version must not let the check be skipped (fail closed).
        BannerZone zone = (await GetRequiredService<IBannerZoneService>().GetAsync(_zoneKey))!;
        var banner = new Banner
        {
            Key = Guid.NewGuid(),
            Name = "Live only html",
            Type = BannerType.Html,
            IsPublished = true,
            CreateDate = DateTime.UtcNow,
            UpdateDate = DateTime.UtcNow,
            CreatedBy = Constants.Security.SuperUserKey,
            UpdatedBy = Constants.Security.SuperUserKey,
            Live = new BannerVersion { Kind = BannerVersionKind.Live, Html = StoredHtml, Weight = 1, SavedDate = DateTime.UtcNow, SavedBy = Constants.Security.SuperUserKey, ZoneIds = [zone.Id] },
        };
        using (IScope scope = ScopeProvider.CreateScope())
        {
            GetRequiredService<IBannerRepository>().Add(banner);
            scope.Complete();
        }

        await SignInWithGroupsAsync("no-html-no-draft@test.invalid", (Section: true, Html: false));

        await AssertHtmlPermissionRequiredAsync(await PutAsync("/banners/" + banner.Key, HtmlBody("<p>Changed</p>", updateDate: DateTime.UtcNow)));
    }

    [Test]
    public async Task TheService_RefusesEveryHtmlWrite_ForAUserWithoutTheVerb()
    {
        Guid key = await HtmlBannerCreatedByAdminAsync();
        await SignInWithGroupsAsync("no-html-service@test.invalid", (Section: true, Html: false));
        Guid user = UserKeyOf("no-html-service@test.invalid");
        IBannerService service = GetRequiredService<IBannerService>();
        Banner stored = (await service.GetAsync(key))!;
        var draft = new BannerDraftModel { Name = "Html offer", Html = "<p>Changed</p>", Weight = 1, ZoneKeys = [_zoneKey] };

        var create = await service.CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = draft }, user);
        draft.UpdateDate = stored.UpdateDate;
        var save = await service.SaveDraftAsync(key, draft, user);

        Assert.Multiple(() =>
        {
            Assert.That(create.Status, Is.EqualTo(BannerOperationStatus.HtmlPermissionRequired));
            Assert.That(save.Status, Is.EqualTo(BannerOperationStatus.HtmlPermissionRequired));
        });
    }

    [Test]
    public async Task TheService_AllowsHtmlWrites_ForAUserWithTheVerb()
    {
        await SignInWithGroupsAsync("html-service@test.invalid", (Section: true, Html: true));
        var draft = new BannerDraftModel { Name = "Html offer", Html = StoredHtml, Weight = 1, ZoneKeys = [_zoneKey] };

        var create = await GetRequiredService<IBannerService>().CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = draft }, UserKeyOf("html-service@test.invalid"));

        Assert.That(create.Success, Is.True, create.Status.ToString());
    }

    [Test]
    public async Task WithoutTheVerb_TheTypeChangeAloneToHtml_Gives403_AndWithTheVerb400()
    {
        // Empty html isolates the type clause of the check from the html comparison.
        Guid key = await CreateBannerAsync(NewMediaKey(), _zoneKey);
        JsonElement banner = await ReadAsync(await GetAsync("/banners/" + key));
        object body = HtmlBody(html: "", updateDate: banner.GetProperty("updateDate").GetDateTime());
        Assert.That((await PutAsync("/banners/" + key, body)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "administrator: the type is fixed");

        await SignInWithGroupsAsync("no-html-type-only@test.invalid", (Section: true, Html: false));

        await AssertHtmlPermissionRequiredAsync(await PutAsync("/banners/" + key, body));
    }

    [Test]
    public async Task WithoutTheVerb_AnImageBannerWithHtmlText_CannotBeCreated_AndNullHtmlIsNotAChange()
    {
        await SignInWithGroupsAsync("no-html-image-create@test.invalid", (Section: true, Html: false));
        string withHtml = JsonSerializer.Serialize(BannerBody(NewMediaKey(), [_zoneKey]), Json).Replace("\"weight\":1", "\"weight\":1,\"html\":\"<b>x</b>\"");

        HttpResponseMessage refused = await Client.PostAsync(Root + "/banners", new StringContent(withHtml, System.Text.Encoding.UTF8, "application/json"));
        HttpResponseMessage allowed = await PostAsync("/banners", BannerBody(NewMediaKey(), [_zoneKey]));

        await AssertHtmlPermissionRequiredAsync(refused);
        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task TheService_FailsClosed_ForAnUnknownUser()
    {
        var draft = new BannerDraftModel { Name = "Html offer", Html = StoredHtml, Weight = 1, ZoneKeys = [_zoneKey] };

        var attempt = await GetRequiredService<IBannerService>().CreateAsync(new BannerCreateModel { Type = BannerType.Html, Draft = draft }, Guid.NewGuid());

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.HtmlPermissionRequired));
    }
}
