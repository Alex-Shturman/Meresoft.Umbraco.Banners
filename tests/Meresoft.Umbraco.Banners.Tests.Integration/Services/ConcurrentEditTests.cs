using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// The stale check must hold when another editor saves between the read and the write of a request.
/// Real threads cannot be used here: the SQLite test database stalls on overlapping write scopes, so the
/// competing save is run at exactly that point, from a repository decorator, inside the same transaction.
/// Real simultaneity (two connections, one blocked on the other's row lock) is covered only by
/// <see cref="SqlServerConcurrentEditTests"/>, which needs a SQL Server.
/// </summary>
[TestFixture]
public class ConcurrentEditTests : BannersServiceTestBase
{
    /// <summary>Runs once, right after the next banner or zone read, as if another editor saved then.</summary>
    private static Action? _afterRead;

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IBannerRepository>(sp => new CompetingBannerRepository(ActivatorUtilities.CreateInstance<BannerRepository>(sp)));
        builder.Services.AddSingleton<IZoneRepository>(sp => new CompetingZoneRepository(ActivatorUtilities.CreateInstance<ZoneRepository>(sp)));
    }

    [TearDown]
    public void ClearHook() => _afterRead = null;

    [Test]
    public async Task SaveDraft_WhenAnotherEditorSavesBetweenReadAndWrite_ReturnsChangedByAnotherUser_AndWritesNothing()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));
        var bannerRepository = GetRequiredService<IBannerRepository>();
        _afterRead = () =>
        {
            _afterRead = null;
            Banner other = bannerRepository.GetByKey(banner.Key)!;
            other.Name = "Editor A wins";
            other.UpdateDate = Clock.GetUtcNow().UtcDateTime;
            Assert.That(bannerRepository.SaveDraft(other, banner.UpdateDate), Is.True);
        };

        BannerDraftModel mine = ImageDraft(banner.Draft!.MediaKey!.Value, zone.Key);
        mine.Name = "Editor B loses";
        mine.AltText = "Written by the loser";
        var attempt = await BannerService.SaveDraftAsync(banner.Key, mine, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
        Banner stored = (await BannerService.GetAsync(banner.Key))!;
        // The competing save ran in the same transaction, which the failed request rolls back, so only
        // "nothing of the loser was written" can be seen here.
        Assert.That(stored.Name, Is.EqualTo("Summer promo"));
        Assert.That(stored.Draft!.AltText, Is.EqualTo("Invented summer promo"), "the version row was not written");
    }

    [Test]
    public async Task Publish_WhenAnotherEditorSavesBetweenReadAndWrite_ReturnsChangedByAnotherUser_AndPublishesNothing()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        Clock.Advance(TimeSpan.FromMinutes(1));
        var bannerRepository = GetRequiredService<IBannerRepository>();
        _afterRead = () =>
        {
            _afterRead = null;
            Banner other = bannerRepository.GetByKey(banner.Key)!;
            other.UpdateDate = Clock.GetUtcNow().UtcDateTime;
            Assert.That(bannerRepository.SaveDraft(other, banner.UpdateDate), Is.True);
        };

        var attempt = await BannerService.PublishAsync(banner.Key, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
        Banner stored = (await BannerService.GetAsync(banner.Key))!;
        Assert.That(stored.IsPublished, Is.False);
        Assert.That(stored.Live, Is.Null);
    }

    [Test]
    public async Task UpdateZone_WhenAnotherEditorSavesBetweenReadAndWrite_ReturnsChangedByAnotherUser()
    {
        BannerZone zone = await NewZoneAsync("racing");
        Clock.Advance(TimeSpan.FromMinutes(1));
        var zoneRepository = GetRequiredService<IZoneRepository>();
        _afterRead = () =>
        {
            _afterRead = null;
            BannerZone other = zoneRepository.GetByKey(zone.Key)!;
            other.Name = "Editor A wins";
            other.UpdateDate = Clock.GetUtcNow().UtcDateTime;
            Assert.That(zoneRepository.Update(other, zone.UpdateDate), Is.True);
        };

        var attempt = await ZoneService.UpdateAsync(
            zone.Key,
            new BannerZoneModel { Alias = "racing", Name = "Editor B loses", ShowCount = 1, UpdateDate = zone.UpdateDate },
            User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.ChangedByAnotherUser));
        Assert.That((await ZoneService.GetByAliasAsync("racing"))!.Name, Is.Not.EqualTo("Editor B loses"), "nothing of the loser was written");
    }

    private sealed class CompetingBannerRepository : IBannerRepository
    {
        private readonly IBannerRepository _inner;

        public CompetingBannerRepository(IBannerRepository inner) => _inner = inner;

        public Banner? GetByKey(Guid key)
        {
            Banner? banner = _inner.GetByKey(key);
            Action? hook = _afterRead;
            _afterRead = null;
            hook?.Invoke();
            return banner;
        }

        public Banner Add(Banner banner) => _inner.Add(banner);
        public Banner? GetById(int id) => _inner.GetById(id);
        public void SaveDraft(Banner banner) => _inner.SaveDraft(banner);
        public bool SaveDraft(Banner banner, DateTime expectedUpdateDate) => _inner.SaveDraft(banner, expectedUpdateDate);
        public void CopyDraftOverLive(int bannerId, DateTime publishDateUtc) => _inner.CopyDraftOverLive(bannerId, publishDateUtc);
        public bool CopyDraftOverLive(int bannerId, DateTime publishDateUtc, DateTime expectedUpdateDate) => _inner.CopyDraftOverLive(bannerId, publishDateUtc, expectedUpdateDate);
        public void SetPublished(int bannerId, bool isPublished) => _inner.SetPublished(bannerId, isPublished);
        public void Delete(int id) => _inner.Delete(id);
        public IReadOnlyList<Banner> GetAllPublished() => _inner.GetAllPublished();
        public (IReadOnlyList<Banner> Items, long Total) GetPaged(BannerFilter filter) => _inner.GetPaged(filter);
    }

    private sealed class CompetingZoneRepository : IZoneRepository
    {
        private readonly IZoneRepository _inner;

        public CompetingZoneRepository(IZoneRepository inner) => _inner = inner;

        public BannerZone? GetByKey(Guid key)
        {
            BannerZone? zone = _inner.GetByKey(key);
            Action? hook = _afterRead;
            _afterRead = null;
            hook?.Invoke();
            return zone;
        }

        public BannerZone Add(BannerZone zone) => _inner.Add(zone);
        public void Update(BannerZone zone) => _inner.Update(zone);
        public bool Update(BannerZone zone, DateTime expectedUpdateDate) => _inner.Update(zone, expectedUpdateDate);
        public void Delete(int id) => _inner.Delete(id);
        public BannerZone? GetByAlias(string alias) => _inner.GetByAlias(alias);
        public BannerZone? GetById(int id) => _inner.GetById(id);
        public IReadOnlyList<BannerZone> GetAll() => _inner.GetAll();
        public IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId) => _inner.GetAssignedBannerKeys(zoneId);
    }
}
