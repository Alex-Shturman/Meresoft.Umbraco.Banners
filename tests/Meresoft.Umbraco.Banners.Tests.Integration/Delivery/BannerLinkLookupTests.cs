using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Delivery;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

/// <summary>Where a click goes, and what the lookup costs the database.</summary>
[TestFixture]
public class BannerLinkLookupTests : BannersServiceTestBase
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private BannerZone _zone = null!;

    [SetUp]
    public async Task Prepare() => _zone = await NewZoneAsync("home-top");

    private IBannerLinkLookup Lookup => GetRequiredService<IBannerLinkLookup>();

    /// <summary>A banner that was published and then unpublished: its live version stays, so it comes from the database path.</summary>
    private async Task<Banner> UnpublishedBannerAsync()
    {
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), _zone.Key));
        Assert.That((await BannerService.PublishAsync(banner.Key, User)).Success, Is.True);
        Assert.That((await BannerService.UnpublishAsync(banner.Key, User)).Success, Is.True);
        return banner;
    }

    /// <summary>Counts the database commands a piece of work issues.</summary>
    private int Queries(Action work)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase database = scope.Database;
        database.EnableSqlCount = true;
        int before = database.SqlCount;
        work();
        return database.SqlCount - before;
    }

    // Review 9: one link check on both paths.

    [TestCase("javascript:alert(1)")]
    [TestCase("ftp://example.org/file")]
    [TestCase("/relative/path")]
    [TestCase("https:///no-host")]
    public async Task ASnapshotBannerWithALinkThatIsNotAbsoluteHttp_GivesNoTarget(string link)
    {
        var banner = new Banner
        {
            Key = Guid.NewGuid(),
            Name = "Odd link",
            Type = BannerType.Image,
            CreateDate = Clock.GetUtcNow().UtcDateTime,
            UpdateDate = Clock.GetUtcNow().UtcDateTime,
            CreatedBy = User,
            UpdatedBy = User,
            Draft = new BannerVersion
            {
                Kind = BannerVersionKind.Draft,
                MediaKey = NewMediaKey(),
                LinkUrl = link,
                AltText = "Invented",
                Weight = 1,
                SavedDate = Clock.GetUtcNow().UtcDateTime,
                SavedBy = User,
                ZoneIds = [_zone.Id],
            },
        };
        using (IScope scope = ScopeProvider.CreateScope())
        {
            var repository = GetRequiredService<IBannerRepository>();
            repository.Add(banner);
            repository.CopyDraftOverLive(banner.Id, Clock.GetUtcNow().UtcDateTime);
            scope.Complete();
        }

        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();
        Assert.That(
            GetRequiredService<ILiveSnapshotAccessor>().Current.Zones["home-top"].Candidates.Any(c => c.BannerId == banner.Id),
            Is.True,
            "the banner is in the snapshot, so this checks the snapshot path");

        Assert.That(Lookup.Find(banner.Id, _zone.Id), Is.Null);
    }

    // Review 2: a bounded cache that is never cleared as a whole, and a cheap reject of ids that cannot exist.

    [Test]
    public async Task AnIdAboveEveryKnownBanner_IsRejectedWithoutReadingTheBannerFromTheDatabase()
    {
        Banner known = await UnpublishedBannerAsync();
        Assert.That(Lookup.Find(known.Id, _zone.Id), Is.Not.Null, "warm up: the highest known id is read once");

        int queries = Queries(() =>
        {
            Assert.That(Lookup.Find(known.Id + 1_000, _zone.Id), Is.Null);
            Assert.That(Lookup.Find(known.Id + 1_001, _zone.Id), Is.Null);
            Assert.That(Lookup.Find(int.MaxValue, null), Is.Null);
        });

        Assert.That(queries, Is.EqualTo(0));
    }

    [Test]
    public async Task AKnownBanner_IsCached_SoTheSecondClickReadsNothing()
    {
        Banner banner = await UnpublishedBannerAsync();
        Assert.That(Lookup.Find(banner.Id, _zone.Id), Is.Not.Null);

        Assert.That(Queries(() => Lookup.Find(banner.Id, _zone.Id)), Is.EqualTo(0));
        Assert.That(Lookup.Find(banner.Id, _zone.Id)!.Countable, Is.False, "unpublished: redirected but not counted");
    }

    [Test]
    public async Task AFullCache_IsNotCleared_ANewEntryIsSimplyNotCachedUntilOldOnesExpire()
    {
        Banner a = await UnpublishedBannerAsync();
        Banner b = await UnpublishedBannerAsync();
        Banner c = await UnpublishedBannerAsync();
        IBannerLinkLookup lookup = ActivatorUtilities.CreateInstance<BannerLinkLookup>(Services, 2);
        lookup.Find(a.Id, _zone.Id);
        lookup.Find(b.Id, _zone.Id);

        Assert.That(Queries(() => lookup.Find(c.Id, _zone.Id)), Is.GreaterThan(0), "read from the database");
        Assert.That(Queries(() => lookup.Find(c.Id, _zone.Id)), Is.GreaterThan(0), "the cache is full of live entries, so c is not kept");
        Assert.That(Queries(() => lookup.Find(a.Id, _zone.Id)), Is.EqualTo(0), "a is still cached: nothing was cleared");
        Assert.That(Queries(() => lookup.Find(b.Id, _zone.Id)), Is.EqualTo(0), "b is still cached: nothing was cleared");

        Clock.Advance(CacheLifetime + TimeSpan.FromSeconds(1));

        Assert.That(Queries(() => lookup.Find(c.Id, _zone.Id)), Is.GreaterThan(0), "a and b expired, so there is room for c");
        Assert.That(Queries(() => lookup.Find(c.Id, _zone.Id)), Is.EqualTo(0), "and c is kept now");
    }
}
