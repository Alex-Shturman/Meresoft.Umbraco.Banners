using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Persistence;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>FR-018: every change is recorded in the Umbraco audit, and the history endpoints read it back.</summary>
[TestFixture]
public class AuditTests : BannersApiTestBase
{
    private Guid _zoneKey;
    private Guid _media;

    [SetUp]
    public async Task Prepare()
    {
        (_zoneKey, _) = await CreateZoneAsync("audit-zone");
        _media = NewMediaKey();
    }

    private async Task<JsonElement> HistoryAsync(string kind, Guid key, string query = "")
    {
        HttpResponseMessage response = await GetAsync($"/{kind}/{key}/history{query}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return await ReadAsync(response);
    }

    private static string[] Actions(JsonElement history) =>
        history.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()!).ToArray();

    private async Task<int> TotalAsync(string kind, Guid key) => (await HistoryAsync(kind, key)).GetProperty("total").GetInt32();

    /// <summary>The audit rows written for one entity, read from the audit table.</summary>
    private Task<StoredAuditRow[]> StoredAsync(int entityId, string entityType) =>
        Task.FromResult(StoredAudit.For(ScopeProvider, entityId, entityType));

    private async Task<int> BannerIdAsync(Guid key) => (await GetRequiredService<IBannerService>().GetAsync(key))!.Id;

    [Test]
    public async Task BannerLifecycle_WritesOneEntryPerAction_NewestFirst_WithUserAndDate()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        JsonElement loaded = await ReadAsync(await GetAsync("/banners/" + key));
        object body = BannerBody(_media, [_zoneKey], loaded.GetProperty("updateDate").GetDateTime(), name: "Renamed promo");

        Assert.That((await PutAsync("/banners/" + key, body)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PublishAsync(key)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PostAsync($"/banners/{key}/unpublish")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PostAsync($"/banners/{key}/copy")).StatusCode, Is.EqualTo(HttpStatusCode.Created));

        JsonElement history = await HistoryAsync("banners", key);
        JsonElement newest = history.GetProperty("items")[0];
        Assert.Multiple(() =>
        {
            Assert.That(Actions(history), Is.EqualTo(new[] { "unpublish", "publish", "save", "new" }), "the copy is a different banner");
            Assert.That(history.GetProperty("total").GetInt32(), Is.EqualTo(4));
            Assert.That(newest.GetProperty("userName").GetString(), Is.Not.Empty);
            Assert.That(newest.GetProperty("date").GetDateTime(), Is.Not.EqualTo(default(DateTime)));
        });
    }

    [Test]
    public async Task EachBannerAction_AddsExactlyOneEntry()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        int total = await TotalAsync("banners", key);

        await PublishAsync(key);
        Assert.That(await TotalAsync("banners", key), Is.EqualTo(total + 1), "publish");
        await PostAsync($"/banners/{key}/unpublish");
        Assert.That(await TotalAsync("banners", key), Is.EqualTo(total + 2), "unpublish");
    }

    [Test]
    public async Task CopyAndDelete_WriteOneEntryEach_OnTheAffectedBanner()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        Guid copyKey = KeyFromLocation(await PostAsync($"/banners/{key}/copy"));
        int copyId = await BannerIdAsync(copyKey);

        Assert.That(Actions(await HistoryAsync("banners", copyKey)), Is.EqualTo(new[] { "copy" }));

        await DeleteAsync("/banners/" + copyKey);
        Assert.That((await StoredAsync(copyId, BannersConstants.BannerEntityType)).Select(s => s.AuditType), Does.Contain(AuditType.Delete));
    }

    [Test]
    public async Task TheStoredEntry_HasTheUser_TheEntityType_AndACommentWithOnlyIdAndAction()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        int id = await BannerIdAsync(key);

        StoredAuditRow entry = (await StoredAsync(id, BannersConstants.BannerEntityType)).Single();

        IUser admin = GetRequiredService<IUserService>().GetByEmail("banners-admin@test.invalid")!;
        Assert.Multiple(() =>
        {
            Assert.That(entry.UserId, Is.EqualTo(admin.Id));
            Assert.That(entry.EntityType, Is.EqualTo("meresoft-banner"));
            Assert.That(entry.AuditType, Is.EqualTo(AuditType.New));
            Assert.That(entry.Comment, Is.EqualTo($"new banner {id}"), "ids and action names only, never content");
        });
    }

    [Test]
    public async Task ZoneCreateUpdateDelete_WriteOneEntryEach_AndHistoryIsReadable()
    {
        JsonElement zone = await ReadAsync(await GetAsync("/zones/" + _zoneKey));
        object update = ZoneBody("audit-zone", zone.GetProperty("updateDate").GetDateTime(), showCount: 2);
        Assert.That((await PutAsync("/zones/" + _zoneKey, update)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        JsonElement history = await HistoryAsync("zones", _zoneKey);
        Assert.That(Actions(history), Is.EqualTo(new[] { "save", "new" }));
        Assert.That(history.GetProperty("items")[0].GetProperty("userName").GetString(), Is.Not.Empty);

        int id = (await GetRequiredService<IBannerZoneService>().GetAsync(_zoneKey))!.Id;
        Assert.That((await DeleteAsync("/zones/" + _zoneKey)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await StoredAsync(id, BannersConstants.ZoneEntityType)).Select(s => s.AuditType), Does.Contain(AuditType.Delete));
    }

    [Test]
    public async Task UnpublishThenPublish_WithNoEdit_RecordsNoSavedEntry()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await PublishAsync(key);
        await PostAsync($"/banners/{key}/unpublish");
        JsonElement loaded = await ReadAsync(await GetAsync("/banners/" + key));
        DateTime updateDate = loaded.GetProperty("updateDate").GetDateTime();

        // The editor's Publish saves the draft first; nothing was edited, so the body equals the stored draft.
        HttpResponseMessage save = await PutAsync("/banners/" + key, BannerBody(_media, [_zoneKey], updateDate));
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK), await save.Content.ReadAsStringAsync());
        Assert.That((await PublishAsync(key)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "publish", "unpublish", "publish", "new" }));
    }

    [Test]
    public async Task SavingAnUnchangedBanner_DoesNotMoveTheUpdateDate()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        JsonElement loaded = await ReadAsync(await GetAsync("/banners/" + key));
        DateTime updateDate = loaded.GetProperty("updateDate").GetDateTime();

        HttpResponseMessage save = await PutAsync("/banners/" + key, BannerBody(_media, [_zoneKey], updateDate));
        JsonElement saved = await ReadAsync(save);

        Assert.Multiple(() =>
        {
            Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(saved.GetProperty("updateDate").GetDateTime(), Is.EqualTo(updateDate));
        });
        Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "new" }));
    }

    [Test]
    public async Task EditingABanner_ThenPublishing_RecordsSavedAndPublished()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        JsonElement loaded = await ReadAsync(await GetAsync("/banners/" + key));
        object body = BannerBody(_media, [_zoneKey], loaded.GetProperty("updateDate").GetDateTime(), name: "Edited promo");

        Assert.That((await PutAsync("/banners/" + key, body)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await PublishAsync(key)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "publish", "save", "new" }));
    }

    [Test]
    public async Task SavingAnUnchangedZone_RecordsNoSavedEntry_AndKeepsTheUpdateDate()
    {
        JsonElement zone = await ReadAsync(await GetAsync("/zones/" + _zoneKey));
        DateTime updateDate = zone.GetProperty("updateDate").GetDateTime();

        HttpResponseMessage save = await PutAsync("/zones/" + _zoneKey, ZoneBody("audit-zone", updateDate));
        JsonElement saved = await ReadAsync(save);

        Assert.Multiple(() =>
        {
            Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(saved.GetProperty("updateDate").GetDateTime(), Is.EqualTo(updateDate));
        });
        Assert.That(Actions(await HistoryAsync("zones", _zoneKey)), Is.EqualTo(new[] { "new" }));
    }

    /// <summary>The body of the editor for an image banner, as a dictionary, so a test can change one field.</summary>
    private Dictionary<string, object?> ImageFields(Guid mediaKey, Guid[] zoneKeys, DateTime? updateDate) => new()
    {
        ["name"] = "Summer promo",
        ["type"] = "image",
        ["mediaKey"] = mediaKey,
        ["linkUrl"] = "https://example.org/show",
        ["openInNewWindow"] = false,
        ["altText"] = "Invented summer promo",
        ["title"] = null,
        ["weight"] = 1,
        ["startUtc"] = null,
        ["endUtc"] = null,
        ["impressionLimit"] = null,
        ["clickLimit"] = null,
        ["zoneKeys"] = zoneKeys,
        ["updateDate"] = updateDate,
    };

    private async Task<DateTime> UpdateDateAsync(string path) =>
        (await ReadAsync(await GetAsync(path))).GetProperty("updateDate").GetDateTime();

    /// <summary>A real change is saved: the answer is 200, a "save" entry is written and the update date moves.</summary>
    private async Task AssertSavedAsync(Guid key, object body, DateTime before)
    {
        HttpResponseMessage save = await PutAsync("/banners/" + key, body);
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK), await save.Content.ReadAsStringAsync());
        Assert.Multiple(async () =>
        {
            Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "save", "new" }));
            Assert.That(await UpdateDateAsync("/banners/" + key), Is.GreaterThan(before));
        });
    }

    [TestCase("weight", 2)]
    [TestCase("startUtc", "2027-04-01T00:00:00Z")]
    [TestCase("endUtc", "2027-05-01T00:00:00Z")]
    [TestCase("impressionLimit", 100)]
    [TestCase("clickLimit", 10)]
    [TestCase("openInNewWindow", true)]
    [TestCase("title", "Invented title")]
    [TestCase("altText", "Another invented text")]
    [TestCase("linkUrl", "https://example.org/other")]
    [TestCase("mediaKey", null)]
    public async Task ChangingOneField_IsSaved_RecordsSaved_AndMovesTheUpdateDate(string field, object? value)
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        DateTime before = await UpdateDateAsync("/banners/" + key);
        Dictionary<string, object?> fields = ImageFields(_media, [_zoneKey], before);
        fields[field] = field == "mediaKey" ? NewMediaKey() : value;
        await Task.Delay(50); // The update date has a resolution of 10 ms.

        await AssertSavedAsync(key, fields, before);
    }

    [Test]
    public async Task ChangingTheZoneSet_IsSaved_RecordsSaved_AndMovesTheUpdateDate()
    {
        (Guid otherZone, _) = await CreateZoneAsync("audit-zone-two");
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        DateTime before = await UpdateDateAsync("/banners/" + key);
        await Task.Delay(50);

        await AssertSavedAsync(key, ImageFields(_media, [_zoneKey, otherZone], before), before);
    }

    [Test]
    public async Task ReorderedZoneList_IsNotAChange()
    {
        (Guid otherZone, _) = await CreateZoneAsync("audit-zone-two");
        Guid key = await CreateBannerAsync(_media, _zoneKey, otherZone);
        DateTime before = await UpdateDateAsync("/banners/" + key);

        HttpResponseMessage save = await PutAsync("/banners/" + key, ImageFields(_media, [otherZone, _zoneKey], before));

        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK), await save.Content.ReadAsStringAsync());
        Assert.Multiple(async () =>
        {
            Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "new" }));
            Assert.That(await UpdateDateAsync("/banners/" + key), Is.EqualTo(before));
        });
    }

    private object HtmlFields(string html, DateTime? updateDate = null) => new
    {
        name = "Html offer",
        type = "html",
        html,
        altText = "",
        weight = 1,
        openInNewWindow = false,
        zoneKeys = new[] { _zoneKey },
        updateDate,
    };

    private async Task<Guid> HtmlBannerAsync(string html)
    {
        HttpResponseMessage created = await PostAsync("/banners", HtmlFields(html));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        return KeyFromLocation(created);
    }

    [Test]
    public async Task ChangingOnlyWhitespaceInTheHtml_IsSaved_ForAUserWithThePermission()
    {
        Guid key = await HtmlBannerAsync("<p>Invented html</p>");
        DateTime before = await UpdateDateAsync("/banners/" + key);
        await Task.Delay(50);

        await AssertSavedAsync(key, HtmlFields("<p>Invented html</p>\n", before), before);
    }

    [Test]
    public async Task WithoutTheHtmlPermission_AnUnchangedBody_IsANoOp_AndAWhitespaceChangeIsRefused()
    {
        Guid key = await HtmlBannerAsync("<p>Invented html</p>");
        DateTime before = await UpdateDateAsync("/banners/" + key);
        await SignInWithGroupsAsync("no-html-audit@test.invalid", (Section: true, Html: false));

        HttpResponseMessage unchanged = await PutAsync("/banners/" + key, HtmlFields("<p>Invented html</p>", before));
        HttpResponseMessage whitespace = await PutAsync("/banners/" + key, HtmlFields("<p>Invented html</p> ", before));

        Assert.Multiple(async () =>
        {
            Assert.That(unchanged.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(whitespace.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That((await ReadAsync(whitespace)).GetProperty("type").GetString(), Does.EndWith("html-permission-required"));
            Assert.That(Actions(await HistoryAsync("banners", key)), Is.EqualTo(new[] { "new" }));
        });
    }

    [Test]
    public async Task AStaleUpdateDate_WithABodyIdenticalToTheStoredDraft_StillGivesChangedByAnotherUser()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        DateTime stale = await UpdateDateAsync("/banners/" + key);
        await Task.Delay(50);
        Dictionary<string, object?> edited = ImageFields(_media, [_zoneKey], stale);
        edited["name"] = "Edited promo";
        Assert.That((await PutAsync("/banners/" + key, edited)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // The body now equals the stored draft, but it was loaded before the edit.
        HttpResponseMessage save = await PutAsync("/banners/" + key, edited);

        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await save.Content.ReadAsStringAsync());
        Assert.That((await ReadAsync(save)).GetProperty("type").GetString(), Does.Contain("changed-by-another-user"));
    }

    [Test]
    public async Task History_IsPaged()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await PublishAsync(key);
        await PostAsync($"/banners/{key}/unpublish");

        JsonElement page = await HistoryAsync("banners", key, "?skip=1&take=1");

        Assert.Multiple(() =>
        {
            Assert.That(page.GetProperty("total").GetInt32(), Is.EqualTo(3));
            Assert.That(Actions(page), Is.EqualTo(new[] { "publish" }));
        });
    }

    [Test]
    public async Task History_OfAnUnknownBannerOrZone_Gives404()
    {
        Assert.That((await GetAsync($"/banners/{Guid.NewGuid()}/history")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await GetAsync($"/zones/{Guid.NewGuid()}/history")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task History_NeedsTheBannersSection()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        await SignInWithGroupsAsync("no-section-history@test.invalid", (Section: false, Html: false));

        Assert.That((await GetAsync($"/banners/{key}/history")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await GetAsync($"/zones/{_zoneKey}/history")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    /// <summary>Writes audit rows straight to Umbraco's audit service. Rows of other entity types use the plain id, as Umbraco does for content with the same id; package rows use the mapped node id.</summary>
    private async Task AddAuditRowsAsync(int count, int entityId, string entityType, AuditType type = AuditType.Save)
    {
        IAuditService audit = GetRequiredService<IAuditService>();
        bool isPackageRow = entityType is BannersConstants.BannerEntityType or BannersConstants.ZoneEntityType;
        int nodeId = isPackageRow ? AuditNodeId.FromEntityId(entityId) : entityId;
        for (int i = 0; i < count; i++)
        {
            await audit.AddAsync(type, Constants.Security.SuperUserKey, nodeId, entityType, "invented row", null);
        }
    }

    [Test]
    public async Task History_IgnoresManyRowsOfAnotherEntityTypeWithTheSameId()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        int id = await BannerIdAsync(key);
        await AddAuditRowsAsync(250, id, "Document");

        JsonElement history = await HistoryAsync("banners", key);

        Assert.Multiple(() =>
        {
            Assert.That(history.GetProperty("total").GetInt32(), Is.EqualTo(1), "only the banner's own row");
            Assert.That(Actions(history), Is.EqualTo(new[] { "new" }));
        });
    }

    [Test]
    public async Task History_PagesStablyThroughMoreRowsThanOneChunk_WithOtherRowsBetween()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);
        int id = await BannerIdAsync(key);
        await AddAuditRowsAsync(130, id, BannersConstants.BannerEntityType, AuditType.Publish);
        await AddAuditRowsAsync(130, id, "Media");
        await AddAuditRowsAsync(130, id, BannersConstants.BannerEntityType, AuditType.Unpublish);

        var seen = new List<string>();
        long total = 0;
        for (int skip = 0; ; skip += 100)
        {
            JsonElement page = await HistoryAsync("banners", key, $"?skip={skip}&take=100");
            total = page.GetProperty("total").GetInt64();
            string[] actions = Actions(page);
            seen.AddRange(actions);
            if (actions.Length < 100)
            {
                break;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(261), "1 new + 130 + 130, the Media rows are not counted");
            Assert.That(seen, Has.Count.EqualTo(261), "no row skipped or repeated across pages");
            Assert.That(seen.Count(a => a == "publish"), Is.EqualTo(130));
            Assert.That(seen.Count(a => a == "unpublish"), Is.EqualTo(130));
            Assert.That(seen.Count(a => a == "new"), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task History_RejectsAnOutOfRangeSkipOrTake_AndAllowsAPageBeyondTheEnd()
    {
        Guid key = await CreateBannerAsync(_media, _zoneKey);

        Assert.Multiple(async () =>
        {
            Assert.That((await GetAsync($"/banners/{key}/history?skip=10001")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await GetAsync($"/banners/{key}/history?take=0")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await GetAsync($"/banners/{key}/history?take=101")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That((await GetAsync($"/banners/{key}/history?skip=500")).StatusCode, Is.EqualTo(HttpStatusCode.OK), "past the end is an empty page");
        });
    }

    [TestCase("banners")]
    [TestCase("zones")]
    public async Task History_ReportsBothBadPagingFieldsAtOnce(string kind)
    {
        Guid key = kind == "banners" ? await CreateBannerAsync(_media, _zoneKey) : _zoneKey;

        HttpResponseMessage response = await GetAsync($"/{kind}/{key}/history?skip=-5&take=0");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        JsonElement problem = await ReadAsync(response);
        Assert.That(problem.GetProperty("errors").EnumerateArray().SelectMany(e => e.EnumerateObject().Select(f => f.Name)), Is.EquivalentTo(new[] { "$.skip", "$.take" }), problem.ToString());
    }
}
