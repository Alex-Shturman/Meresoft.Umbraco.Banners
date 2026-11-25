using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

[TestFixture]
public class BannerServiceTests : BannersServiceTestBase
{
    private Guid _media;
    private BannerZone _zone = null!;

    [SetUp]
    public async Task CreateZoneAndMedia()
    {
        _zone = await NewZoneAsync("home-top");
        _media = NewMediaKey();
    }

    private Task<int> AuditCountAsync(int bannerId) =>
        GetRequiredService<IAuditService>().GetItemsByEntityAsync(bannerId, 0, 50).ContinueWith(t => (int)t.Result.Total);

    private async Task<Banner> PublishedBannerAsync()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media, _zone.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));
        var published = await BannerService.PublishAsync(banner.Key, User);
        Assert.That(published.Success, Is.True, published.Status.ToString());
        Clock.Advance(TimeSpan.FromMinutes(1));
        return published.Result!;
    }

    [Test]
    public async Task Create_GivesADraftWithItsAssignmentsAndAnAuditEntry()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media, _zone.Key));

        Assert.Multiple(() =>
        {
            Assert.That(banner.Id, Is.GreaterThan(0));
            Assert.That(banner.Name, Is.EqualTo("Summer promo"));
            Assert.That(banner.Type, Is.EqualTo(BannerType.Image));
            Assert.That(banner.State, Is.EqualTo(BannerState.Draft));
            Assert.That(banner.IsPublished, Is.False);
            Assert.That(banner.Live, Is.Null);
            Assert.That(banner.Draft!.ZoneIds, Is.EqualTo(new[] { _zone.Id }));
            Assert.That(banner.Draft.SavedBy, Is.EqualTo(User));
            Assert.That(banner.CreatedBy, Is.EqualTo(User));
        });
        Assert.That(await AuditCountAsync(banner.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_InvalidLink_ReturnsInvalidData()
    {
        BannerDraftModel draft = ImageDraft(_media, _zone.Key);
        draft.LinkUrl = "javascript:alert(1)";

        var attempt = await BannerService.CreateAsync(new BannerCreateModel { Type = BannerType.Image, Draft = draft }, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
    }

    [Test]
    public async Task Create_UnknownZoneKey_ReturnsInvalidData()
    {
        var attempt = await BannerService.CreateAsync(
            new BannerCreateModel { Type = BannerType.Image, Draft = ImageDraft(_media, Guid.NewGuid()) }, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
    }

    [Test]
    public async Task Get_ReturnsNullForAnUnknownKey()
    {
        Assert.That(await BannerService.GetAsync(Guid.NewGuid()), Is.Null);
    }

    [Test]
    public async Task Publish_CopiesDraftFieldsAndAssignmentsToLive()
    {
        Banner banner = await PublishedBannerAsync();

        Assert.Multiple(() =>
        {
            Assert.That(banner.IsPublished, Is.True);
            Assert.That(banner.PublishDate, Is.EqualTo(Clock.GetUtcNow().UtcDateTime.AddMinutes(-1)));
            Assert.That(banner.State, Is.EqualTo(BannerState.Published));
            Assert.That(banner.Live, Is.Not.Null);
            Assert.That(banner.Live!.Kind, Is.EqualTo(BannerVersionKind.Live));
            Assert.That(banner.Live.MediaKey, Is.EqualTo(_media));
            Assert.That(banner.Live.LinkUrl, Is.EqualTo("https://example.org/show"));
            Assert.That(banner.Live.AltText, Is.EqualTo("Invented summer promo"));
            Assert.That(banner.Live.ZoneIds, Is.EqualTo(new[] { _zone.Id }));
        });
    }

    [Test]
    public async Task Publish_WithoutAZone_ReturnsNoZoneAssigned()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media));

        var attempt = await BannerService.PublishAsync(banner.Key, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.NoZoneAssigned));
        Assert.That((await BannerService.GetAsync(banner.Key))!.IsPublished, Is.False);
    }

    [Test]
    public async Task Publish_UnknownKey_ReturnsNotFound()
    {
        var attempt = await BannerService.PublishAsync(Guid.NewGuid(), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.NotFound));
    }

    [Test]
    public async Task Publish_StaleUpdateDate_ReturnsChangedByAnotherUser()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media, _zone.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));

        var attempt = await BannerService.PublishAsync(banner.Key, User, banner.UpdateDate.AddMinutes(-5));

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
    }

    [Test]
    public async Task SaveDraft_AfterPublish_KeepsTheLiveVersion_AndShowsUnpublishedChanges()
    {
        Banner published = await PublishedBannerAsync();
        BannerDraftModel change = ImageDraft(_media, _zone.Key);
        change.AltText = "Changed alt text";
        change.Weight = 7;
        change.UpdateDate = published.UpdateDate;

        var attempt = await BannerService.SaveDraftAsync(published.Key, change, User);

        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        Banner saved = attempt.Result!;
        Assert.Multiple(() =>
        {
            Assert.That(saved.State, Is.EqualTo(BannerState.PublishedWithChanges));
            Assert.That(saved.Draft!.AltText, Is.EqualTo("Changed alt text"));
            Assert.That(saved.Draft.Weight, Is.EqualTo(7));
            Assert.That(saved.Live!.AltText, Is.EqualTo("Invented summer promo"));
            Assert.That(saved.Live.Weight, Is.EqualTo(1));
            Assert.That(saved.UpdateDate, Is.GreaterThan(published.UpdateDate));
        });
    }

    [Test]
    public async Task SaveDraft_DoesNotChangeTheSnapshot()
    {
        Banner published = await PublishedBannerAsync();
        LiveSnapshot before = await RebuiltSnapshotAsync();
        BannerDraftModel change = ImageDraft(_media, _zone.Key);
        change.AltText = "Changed alt text";
        change.UpdateDate = published.UpdateDate;

        await BannerService.SaveDraftAsync(published.Key, change, User);
        LiveSnapshot after = await RebuiltSnapshotAsync();

        Assert.That(after.Zones["home-top"].Candidates.Single().AltText, Is.EqualTo("Invented summer promo"));
        Assert.That(before.Zones["home-top"].Candidates.Single().AltText, Is.EqualTo("Invented summer promo"));
    }

    private async Task<LiveSnapshot> RebuiltSnapshotAsync()
    {
        ILiveSnapshotAccessor accessor = GetRequiredService<ILiveSnapshotAccessor>();
        await accessor.RebuildAsync();
        return accessor.Current;
    }

    [Test]
    public async Task SaveDraft_StaleUpdateDate_ReturnsChangedByAnotherUser()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media, _zone.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));
        BannerDraftModel first = ImageDraft(_media, _zone.Key);
        first.UpdateDate = banner.UpdateDate;
        await BannerService.SaveDraftAsync(banner.Key, first, User);
        Clock.Advance(TimeSpan.FromMinutes(1));

        BannerDraftModel second = ImageDraft(_media, _zone.Key);
        second.UpdateDate = banner.UpdateDate;
        var attempt = await BannerService.SaveDraftAsync(banner.Key, second, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
    }

    [Test]
    public async Task SaveDraft_ChangingTheType_ReturnsTypeChangeNotAllowed()
    {
        Banner banner = await NewBannerAsync(ImageDraft(_media, _zone.Key));
        BannerDraftModel change = ImageDraft(_media, _zone.Key);
        change.Type = BannerType.Html;

        var attempt = await BannerService.SaveDraftAsync(banner.Key, change, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.TypeChangeNotAllowed));
    }

    [Test]
    public async Task SaveDraft_UnknownKey_ReturnsNotFound()
    {
        var attempt = await BannerService.SaveDraftAsync(Guid.NewGuid(), ImageDraft(_media, _zone.Key), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.NotFound));
    }

    [Test]
    public async Task Unpublish_KeepsTheLiveRow_AndGivesUnpublished()
    {
        Banner published = await PublishedBannerAsync();

        var attempt = await BannerService.UnpublishAsync(published.Key, User);

        Assert.That(attempt.Success, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(attempt.Result!.IsPublished, Is.False);
            Assert.That(attempt.Result.State, Is.EqualTo(BannerState.Unpublished));
            Assert.That(attempt.Result.Live, Is.Not.Null);
        });
    }

    [Test]
    public async Task Publish_AfterUnpublish_ShowsAgain()
    {
        Banner published = await PublishedBannerAsync();
        await BannerService.UnpublishAsync(published.Key, User);
        Clock.Advance(TimeSpan.FromMinutes(1));

        var attempt = await BannerService.PublishAsync(published.Key, User);

        Assert.That(attempt.Result!.State, Is.EqualTo(BannerState.Published));
    }

    [Test]
    public async Task Copy_CreatesADraftWithTheDraftFieldsAndAssignments_AndNoStatistics()
    {
        Banner source = await PublishedBannerAsync();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new DailyStatDto { Day = new DateOnly(2027, 3, 1), BannerId = source.Id, ZoneId = _zone.Id, Impressions = 10, Clicks = 1 });
            scope.Complete();
        }

        var attempt = await BannerService.CopyAsync(source.Key, "Summer promo copy", User);

        Assert.That(attempt.Success, Is.True);
        Banner copy = attempt.Result!;
        Assert.Multiple(() =>
        {
            Assert.That(copy.Key, Is.Not.EqualTo(source.Key));
            Assert.That(copy.Name, Is.EqualTo("Summer promo copy"));
            Assert.That(copy.State, Is.EqualTo(BannerState.Draft));
            Assert.That(copy.Live, Is.Null);
            Assert.That(copy.Draft!.MediaKey, Is.EqualTo(_media));
            Assert.That(copy.Draft.ZoneIds, Is.EqualTo(new[] { _zone.Id }));
        });
        using IScope check = ScopeProvider.CreateScope(autoComplete: true);
        Assert.That(check.Database.Fetch<DailyStatDto>().Count(s => s.BannerId == copy.Id), Is.EqualTo(0));
    }

    [Test]
    public async Task Copy_WithoutAName_AddsACopyMarker()
    {
        Banner source = await NewBannerAsync(ImageDraft(_media, _zone.Key));

        var attempt = await BannerService.CopyAsync(source.Key, null, User);

        Assert.That(attempt.Result!.Name, Does.StartWith("Summer promo"));
        Assert.That(attempt.Result.Name, Is.Not.EqualTo("Summer promo"));
    }

    [Test]
    public async Task Delete_RemovesTheBanner_KeepsDailyStats_AndAudits()
    {
        Banner banner = await PublishedBannerAsync();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new DailyStatDto { Day = new DateOnly(2027, 3, 1), BannerId = banner.Id, ZoneId = _zone.Id, Impressions = 10, Clicks = 1 });
            scope.Complete();
        }

        BannerOperationStatus status = await BannerService.DeleteAsync(banner.Key, User);

        Assert.That(status, Is.EqualTo(BannerOperationStatus.Success));
        Assert.That(await BannerService.GetAsync(banner.Key), Is.Null);
        using IScope check = ScopeProvider.CreateScope(autoComplete: true);
        Assert.That(check.Database.Fetch<DailyStatDto>().Count(s => s.BannerId == banner.Id), Is.EqualTo(1));
        Assert.That(await AuditCountAsync(banner.Id), Is.EqualTo(3), "create, publish, delete");
    }

    [Test]
    public async Task Delete_UnknownKey_ReturnsNotFound()
    {
        Assert.That(await BannerService.DeleteAsync(Guid.NewGuid(), User), Is.EqualTo(BannerOperationStatus.NotFound));
    }

    [Test]
    public async Task GetPaged_FiltersByStateZoneAndType_AndPages()
    {
        BannerZone other = await NewZoneAsync("sidebar");
        Banner draft = await NewBannerAsync(ImageDraft(_media, _zone.Key));
        Banner published = await PublishedBannerAsync();
        Banner elsewhere = await NewBannerAsync(ImageDraft(_media, other.Key));

        var all = await BannerService.GetPagedAsync(new BannerQuery { Take = 50 });
        var drafts = await BannerService.GetPagedAsync(new BannerQuery { State = BannerState.Draft });
        var inSidebar = await BannerService.GetPagedAsync(new BannerQuery { ZoneKey = other.Key });
        var html = await BannerService.GetPagedAsync(new BannerQuery { Type = BannerType.Html });
        var firstPage = await BannerService.GetPagedAsync(new BannerQuery { Skip = 0, Take = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(all.Total, Is.EqualTo(3));
            Assert.That(drafts.Items.Select(i => i.Key), Is.EquivalentTo(new[] { draft.Key, elsewhere.Key }));
            Assert.That(inSidebar.Items.Select(i => i.Key), Is.EqualTo(new[] { elsewhere.Key }));
            Assert.That(html.Total, Is.EqualTo(0));
            Assert.That(firstPage.Items.Count(), Is.EqualTo(2));
            Assert.That(firstPage.Total, Is.EqualTo(3));
        });
        BannerListItem item = all.Items.Single(i => i.Key == published.Key);
        Assert.Multiple(() =>
        {
            Assert.That(item.Name, Is.EqualTo("Summer promo"));
            Assert.That(item.Type, Is.EqualTo(BannerType.Image));
            Assert.That(item.State, Is.EqualTo(BannerState.Published));
            Assert.That(item.ZoneKeys, Is.EqualTo(new[] { _zone.Key }));
            Assert.That(item.Weight, Is.EqualTo(1));
            Assert.That(item.UpdateDate, Is.EqualTo(published.UpdateDate));
            Assert.That(item.Flags, Is.EqualTo(BannerFlags.None));
        });
    }

    [Test]
    public async Task GetPaged_FlagsABannerWithoutZoneAsNotPlaced_AndAMissingImage()
    {
        Banner notPlaced = await NewBannerAsync(ImageDraft(_media));
        Banner gone = await NewBannerAsync(ImageDraft(Guid.NewGuid(), _zone.Key));

        var page = await BannerService.GetPagedAsync(new BannerQuery());

        Assert.That(page.Items.Single(i => i.Key == notPlaced.Key).Flags, Is.EqualTo(BannerFlags.NotPlaced));
        Assert.That(page.Items.Single(i => i.Key == gone.Key).Flags, Is.EqualTo(BannerFlags.ImageMissing));
    }
}
