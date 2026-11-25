using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

[TestFixture]
public class BannerZoneServiceTests : BannersServiceTestBase
{
    private static BannerZoneModel Model(string alias) => new()
    {
        Alias = alias,
        Name = "Search page, top",
        Description = "Above the results",
        ShowCount = 2,
        ImageWidth = 728,
        ImageHeight = 90,
        RenderMode = ZoneRenderMode.Server,
    };

    private async Task<int> AuditCountAsync(int zoneId) =>
        (int)(await GetRequiredService<IAuditService>().GetItemsByEntityAsync(zoneId, 0, 50)).Total;

    [Test]
    public async Task Create_StoresTheZoneWithANormalisedAlias_AndWritesAnAuditEntry()
    {
        var attempt = await ZoneService.CreateAsync(Model("  Search-Top "), User);

        Assert.That(attempt.Success, Is.True);
        BannerZone zone = attempt.Result!;
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(zone.Id, Is.GreaterThan(0));
            Assert.That(zone.Key, Is.Not.EqualTo(Guid.Empty));
            Assert.That(zone.Alias, Is.EqualTo("search-top"));
            Assert.That(zone.ShowCount, Is.EqualTo(2));
            Assert.That(zone.CreateDate, Is.EqualTo(Clock.GetUtcNow().UtcDateTime));
            Assert.That((await ZoneService.GetByAliasAsync("search-top"))!.Key, Is.EqualTo(zone.Key));
            Assert.That(await AuditCountAsync(zone.Id), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Create_DuplicateAlias_ReturnsDuplicateAlias()
    {
        await NewZoneAsync("sidebar");

        var attempt = await ZoneService.CreateAsync(Model("Sidebar"), User);

        Assert.That(attempt.Success, Is.False);
        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.DuplicateAlias));
    }

    [Test]
    public async Task Create_InvalidData_ReturnsInvalidData()
    {
        var attempt = await ZoneService.CreateAsync(Model("9-bad alias"), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
    }

    [Test]
    public async Task Create_RemovesTheMatchingRequestedZone()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new RequestedZoneDto { Alias = "home-top", FirstSeen = Clock.GetUtcNow().UtcDateTime, LastSeen = Clock.GetUtcNow().UtcDateTime });
            scope.Database.Insert(new RequestedZoneDto { Alias = "other", FirstSeen = Clock.GetUtcNow().UtcDateTime, LastSeen = Clock.GetUtcNow().UtcDateTime });
            scope.Complete();
        }

        await NewZoneAsync("home-top");

        using IScope check = ScopeProvider.CreateScope(autoComplete: true);
        string[] left = check.Database.Fetch<RequestedZoneDto>().Select(r => r.Alias).ToArray();
        Assert.That(left, Is.EqualTo(new[] { "other" }));
    }

    [Test]
    public async Task Update_ChangesFields_RenamesAndChangesTheAlias_AndAudits()
    {
        BannerZone zone = await NewZoneAsync("old-alias");
        Clock.Advance(TimeSpan.FromMinutes(5));

        BannerZoneModel change = Model("new-alias");
        change.Name = "Renamed";
        change.ShowCount = 4;
        change.UpdateDate = zone.UpdateDate;
        var attempt = await ZoneService.UpdateAsync(zone.Key, change, User);

        Assert.That(attempt.Success, Is.True);
        BannerZone updated = attempt.Result!;
        await Assert.MultipleAsync(async () =>
        {
            Assert.That(updated.Alias, Is.EqualTo("new-alias"));
            Assert.That(updated.Name, Is.EqualTo("Renamed"));
            Assert.That(updated.ShowCount, Is.EqualTo(4));
            Assert.That(updated.UpdateDate, Is.GreaterThan(zone.UpdateDate));
            Assert.That(await ZoneService.GetByAliasAsync("old-alias"), Is.Null);
            Assert.That(await AuditCountAsync(zone.Id), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Update_AliasOfAnotherZone_ReturnsDuplicateAlias()
    {
        await NewZoneAsync("first");
        BannerZone second = await NewZoneAsync("second");

        var attempt = await ZoneService.UpdateAsync(second.Key, Model("first"), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.DuplicateAlias));
    }

    [Test]
    public async Task Update_KeepingItsOwnAlias_Succeeds()
    {
        BannerZone zone = await NewZoneAsync("same");

        var attempt = await ZoneService.UpdateAsync(zone.Key, Model("same"), User);

        Assert.That(attempt.Success, Is.True);
    }

    [Test]
    public async Task Update_StaleUpdateDate_ReturnsChangedByAnotherUser()
    {
        BannerZone zone = await NewZoneAsync("stale");
        Clock.Advance(TimeSpan.FromMinutes(1));
        BannerZoneModel first = Model("stale");
        first.UpdateDate = zone.UpdateDate;
        await ZoneService.UpdateAsync(zone.Key, first, User);
        Clock.Advance(TimeSpan.FromMinutes(1));

        BannerZoneModel second = Model("stale");
        second.UpdateDate = zone.UpdateDate;
        var attempt = await ZoneService.UpdateAsync(zone.Key, second, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
    }

    [Test]
    public async Task Update_UnknownKey_ReturnsNotFound()
    {
        var attempt = await ZoneService.UpdateAsync(Guid.NewGuid(), Model("nothing"), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.NotFound));
    }

    [Test]
    public async Task Delete_RemovesTheZone_AndAudits()
    {
        BannerZone zone = await NewZoneAsync("gone");

        BannerOperationStatus status = await ZoneService.DeleteAsync(zone.Key, User);

        Assert.That(status, Is.EqualTo(BannerOperationStatus.Success));
        Assert.That(await ZoneService.GetByAliasAsync("gone"), Is.Null);
        Assert.That(await AuditCountAsync(zone.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task Delete_UnknownKey_ReturnsNotFound()
    {
        Assert.That(await ZoneService.DeleteAsync(Guid.NewGuid(), User), Is.EqualTo(BannerOperationStatus.NotFound));
    }

    [Test]
    public async Task Delete_WhileADraftIsAssigned_ReturnsZoneInUse_WithTheBannerKeys()
    {
        BannerZone zone = await NewZoneAsync("busy");
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));

        BannerOperationStatus status = await ZoneService.DeleteAsync(zone.Key, User);

        Assert.That(status, Is.EqualTo(BannerOperationStatus.ZoneInUse));
        Assert.That(await ZoneService.GetAssignedBannerKeysAsync(zone.Key), Is.EqualTo(new[] { banner.Key }));
        Assert.That(await ZoneService.GetByAliasAsync("busy"), Is.Not.Null);
    }

    [Test]
    public async Task Delete_WhileOnlyTheLiveVersionIsAssigned_ReturnsZoneInUse()
    {
        BannerZone zone = await NewZoneAsync("live-only");
        BannerZone other = await NewZoneAsync("other");
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        await BannerService.PublishAsync(banner.Key, User);
        Clock.Advance(TimeSpan.FromMinutes(1));
        BannerDraftModel move = ImageDraft(banner.Draft!.MediaKey!.Value, other.Key);
        move.UpdateDate = banner.UpdateDate;
        await BannerService.SaveDraftAsync(banner.Key, move, User);

        BannerOperationStatus status = await ZoneService.DeleteAsync(zone.Key, User);

        Assert.That(status, Is.EqualTo(BannerOperationStatus.ZoneInUse), "the live version still shows in the zone");
    }
}
