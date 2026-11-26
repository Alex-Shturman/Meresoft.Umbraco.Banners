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
