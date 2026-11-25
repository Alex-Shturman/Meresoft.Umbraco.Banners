using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using System.Data.Common;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Persistence;

[TestFixture]
public class ZoneRepositoryTests : BannersIntegrationTestBase
{
    private IZoneRepository Zones => GetRequiredService<IZoneRepository>();
    private IBannerRepository Banners => GetRequiredService<IBannerRepository>();

    [SetUp]
    public async Task InstallSchema() => await InstallPackageAsync();

    private static BannerZone NewZone(string alias = "home-top") => new()
    {
        Key = Guid.NewGuid(),
        Alias = alias,
        Name = "Home top",
        Description = "Invented zone",
        ShowCount = 2,
        ImageWidth = 728,
        ImageHeight = 90,
        RenderMode = ZoneRenderMode.Client,
        CreateDate = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        UpdateDate = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc),
    };

    [Test]
    public void Add_AssignsIdAndRoundTripsAllFields()
    {
        BannerZone zone = NewZone();

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            scope.Complete();
        }

        Assert.That(zone.Id, Is.GreaterThan(0));
        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            BannerZone loaded = Zones.GetByKey(zone.Key)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Id, Is.EqualTo(zone.Id));
                Assert.That(loaded.Alias, Is.EqualTo("home-top"));
                Assert.That(loaded.Name, Is.EqualTo("Home top"));
                Assert.That(loaded.Description, Is.EqualTo("Invented zone"));
                Assert.That(loaded.ShowCount, Is.EqualTo(2));
                Assert.That(loaded.ImageWidth, Is.EqualTo(728));
                Assert.That(loaded.ImageHeight, Is.EqualTo(90));
                Assert.That(loaded.RenderMode, Is.EqualTo(ZoneRenderMode.Client));
                Assert.That(loaded.CreateDate, Is.EqualTo(zone.CreateDate));
            });
        }
    }

    [Test]
    public void GetByAlias_And_GetById_Work()
    {
        BannerZone zone = NewZone();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.Multiple(() =>
            {
                Assert.That(Zones.GetByAlias("home-top")?.Key, Is.EqualTo(zone.Key));
                Assert.That(Zones.GetById(zone.Id)?.Key, Is.EqualTo(zone.Key));
                Assert.That(Zones.GetByAlias("missing"), Is.Null);
                Assert.That(Zones.GetByKey(Guid.NewGuid()), Is.Null);
            });
        }
    }

    [Test]
    public void GetAll_ReturnsAllZonesOrderedByName()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            BannerZone b = NewZone("b-zone"); b.Name = "B";
            BannerZone a = NewZone("a-zone"); a.Name = "A";
            Zones.Add(b);
            Zones.Add(a);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetAll().Select(z => z.Alias), Is.EqualTo(new[] { "a-zone", "b-zone" }));
        }
    }

    [Test]
    public void Update_ChangesFields_IncludingAlias()
    {
        BannerZone zone = NewZone();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            zone.Alias = "home-bottom";
            zone.Name = "Renamed";
            zone.ShowCount = 3;
            Zones.Update(zone);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            BannerZone loaded = Zones.GetByKey(zone.Key)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Alias, Is.EqualTo("home-bottom"));
                Assert.That(loaded.Name, Is.EqualTo("Renamed"));
                Assert.That(loaded.ShowCount, Is.EqualTo(3));
                Assert.That(Zones.GetByAlias("home-top"), Is.Null);
            });
        }
    }

    [Test]
    public void Update_OfAMissingZone_Throws()
    {
        BannerZone ghost = NewZone("ghost");
        ghost.Id = 424242;

        using IScope scope = ScopeProvider.CreateScope();
        Assert.That(() => Zones.Update(ghost), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Update_OfADeletedZone_Throws()
    {
        BannerZone zone = NewZone();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            Zones.Delete(zone.Id);
            scope.Complete();
        }

        using IScope second = ScopeProvider.CreateScope();
        Assert.That(() => Zones.Update(zone), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Delete_RemovesTheZone()
    {
        BannerZone zone = NewZone();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Delete(zone.Id);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetByKey(zone.Key), Is.Null);
        }
    }

    [Test]
    public void DuplicateAlias_IsRejectedByTheDatabase()
    {
        using IScope scope = ScopeProvider.CreateScope();
        Zones.Add(NewZone("same"));

        Assert.That(() => Zones.Add(NewZone("same")), Throws.InstanceOf<DbException>(), "the unique index on Alias must fire");
    }

    [Test]
    public void ZoneWithAssignment_CannotBeDeleted_AndListsTheAssignedBanners()
    {
        BannerZone zone = NewZone();
        Banner banner = BannerRepositoryTests.NewBanner("Invented banner");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            banner.Draft!.ZoneIds = [zone.Id];
            Banners.Add(banner);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetAssignedBannerKeys(zone.Id), Is.EqualTo(new[] { banner.Key }));
        }

        using IScope deleteScope = ScopeProvider.CreateScope();
        Assert.That(() => Zones.Delete(zone.Id), Throws.InstanceOf<DbException>(), "the foreign key from the assignment must block the delete");
    }

    [Test]
    public void AssignedBannerKeys_CountDraftAndLiveVersions_WithoutDuplicates()
    {
        BannerZone zone = NewZone();
        Banner banner = BannerRepositoryTests.NewBanner("Invented banner");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            banner.Draft!.ZoneIds = [zone.Id];
            Banners.Add(banner);
            Banners.CopyDraftOverLive(banner.Id, new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetAssignedBannerKeys(zone.Id), Is.EqualTo(new[] { banner.Key }));
        }
    }

    [Test]
    public void AssignedBannerKeys_IsEmpty_ForUnusedZone()
    {
        BannerZone zone = NewZone();
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetAssignedBannerKeys(zone.Id), Is.Empty);
        }
    }

    [Test]
    public void Update_WithAStaleExpectedUpdateDate_ChangesNothing_AndReturnsFalse()
    {
        var saved = new DateTime(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var zone = new BannerZone { Key = Guid.NewGuid(), Alias = "guarded", Name = "Guarded", CreateDate = saved, UpdateDate = saved };
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Zones.Add(zone);
            scope.Complete();
        }

        zone.Name = "Changed";
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Assert.That(Zones.Update(zone, saved.AddDays(-1)), Is.False);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetById(zone.Id)!.Name, Is.EqualTo("Guarded"));
        }

        zone.UpdateDate = saved.AddHours(1);
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Assert.That(Zones.Update(zone, saved), Is.True);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Zones.GetById(zone.Id)!.Name, Is.EqualTo("Changed"));
        }
    }
}
