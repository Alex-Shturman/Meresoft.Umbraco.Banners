using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Persistence;

[TestFixture]
public class BannerRepositoryTests : BannersIntegrationTestBase
{
    private static readonly DateTime Saved = new(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private IBannerRepository Banners => GetRequiredService<IBannerRepository>();
    private IZoneRepository Zones => GetRequiredService<IZoneRepository>();

    [SetUp]
    public async Task InstallSchema() => await InstallPackageAsync();

    internal static Banner NewBanner(string name, BannerType type = BannerType.Image) => new()
    {
        Key = Guid.NewGuid(),
        Name = name,
        Type = type,
        CreateDate = Saved,
        UpdateDate = Saved,
        CreatedBy = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        UpdatedBy = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        Draft = new BannerVersion
        {
            Kind = BannerVersionKind.Draft,
            MediaKey = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            LinkUrl = "https://example.org/show",
            OpenInNewWindow = true,
            AltText = "Invented boat show",
            Title = "Boat show",
            Weight = 3,
            StartUtc = Saved,
            EndUtc = Saved.AddDays(10),
            ImpressionLimit = 1000,
            ClickLimit = 50,
            SavedDate = Saved,
            SavedBy = Guid.Parse("00000000-0000-0000-0000-0000000000a1"),
        },
    };

    private BannerZone AddZone(string alias)
    {
        var zone = new BannerZone
        {
            Key = Guid.NewGuid(),
            Alias = alias,
            Name = alias,
            CreateDate = Saved,
            UpdateDate = Saved,
        };
        Zones.Add(zone);
        return zone;
    }

    [Test]
    public void Add_RoundTripsBannerDraftAndAssignments()
    {
        Banner banner = NewBanner("Boat show");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            BannerZone zoneA = AddZone("zone-a");
            BannerZone zoneB = AddZone("zone-b");
            banner.Draft!.ZoneIds = [zoneA.Id, zoneB.Id];
            Banners.Add(banner);
            scope.Complete();
        }

        Assert.That(banner.Id, Is.GreaterThan(0));
        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetByKey(banner.Key)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Name, Is.EqualTo("Boat show"));
                Assert.That(loaded.Type, Is.EqualTo(BannerType.Image));
                Assert.That(loaded.IsPublished, Is.False);
                Assert.That(loaded.Live, Is.Null);
                Assert.That(loaded.Draft, Is.Not.Null);
                Assert.That(loaded.Draft!.Kind, Is.EqualTo(BannerVersionKind.Draft));
                Assert.That(loaded.Draft.MediaKey, Is.EqualTo(banner.Draft!.MediaKey));
                Assert.That(loaded.Draft.LinkUrl, Is.EqualTo("https://example.org/show"));
                Assert.That(loaded.Draft.OpenInNewWindow, Is.True);
                Assert.That(loaded.Draft.AltText, Is.EqualTo("Invented boat show"));
                Assert.That(loaded.Draft.Title, Is.EqualTo("Boat show"));
                Assert.That(loaded.Draft.Weight, Is.EqualTo(3));
                Assert.That(loaded.Draft.StartUtc, Is.EqualTo(Saved));
                Assert.That(loaded.Draft.EndUtc, Is.EqualTo(Saved.AddDays(10)));
                Assert.That(loaded.Draft.ImpressionLimit, Is.EqualTo(1000));
                Assert.That(loaded.Draft.ClickLimit, Is.EqualTo(50));
                Assert.That(loaded.Draft.ZoneIds, Is.EquivalentTo(banner.Draft.ZoneIds));
            });
        }
    }

    [Test]
    public void HtmlBanner_RoundTripsLongHtml()
    {
        Banner banner = NewBanner("Html offer", BannerType.Html);
        banner.Draft!.MediaKey = null;
        banner.Draft.LinkUrl = null;
        banner.Draft.AltText = string.Empty;
        banner.Draft.ClickLimit = null;
        banner.Draft.Html = "<p>" + new string('x', 20_000) + "</p>";
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banners.Add(banner);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetById(banner.Id)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Type, Is.EqualTo(BannerType.Html));
                Assert.That(loaded.Draft!.Html, Is.EqualTo(banner.Draft.Html));
                Assert.That(loaded.Draft.MediaKey, Is.Null);
            });
        }
    }

    [Test]
    public void GetByKey_Unknown_ReturnsNull()
    {
        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.Multiple(() =>
            {
                Assert.That(Banners.GetByKey(Guid.NewGuid()), Is.Null);
                Assert.That(Banners.GetById(987654), Is.Null);
            });
        }
    }

    [Test]
    public void SaveDraft_UpdatesDraftAndReplacesAssignments_NotTheLiveVersion()
    {
        Banner banner = NewBanner("Boat show");
        BannerZone zoneA, zoneB;
        using (IScope scope = ScopeProvider.CreateScope())
        {
            zoneA = AddZone("zone-a");
            zoneB = AddZone("zone-b");
            banner.Draft!.ZoneIds = [zoneA.Id];
            Banners.Add(banner);
            Banners.CopyDraftOverLive(banner.Id, Saved.AddDays(1));
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banner edit = Banners.GetByKey(banner.Key)!;
            edit.Name = "Boat show 2";
            edit.Draft!.AltText = "Changed alt";
            edit.Draft.ZoneIds = [zoneB.Id];
            edit.Draft.SavedDate = Saved.AddDays(2);
            Banners.SaveDraft(edit);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetByKey(banner.Key)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Name, Is.EqualTo("Boat show 2"));
                Assert.That(loaded.Draft!.AltText, Is.EqualTo("Changed alt"));
                Assert.That(loaded.Draft.ZoneIds, Is.EqualTo(new[] { zoneB.Id }));
                Assert.That(loaded.Live!.AltText, Is.EqualTo("Invented boat show"));
                Assert.That(loaded.Live.ZoneIds, Is.EqualTo(new[] { zoneA.Id }));
            });
        }
    }

    [Test]
    public void AtMostOneDraftAndOneLiveVersionPerBanner()
    {
        Banner banner = NewBanner("Boat show");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banners.Add(banner);
            scope.Complete();
        }

        using IScope second = ScopeProvider.CreateScope();
        IUmbracoDatabase db = second.Database;
        var duplicate = new BannerVersionDto
        {
            BannerId = banner.Id,
            Kind = (byte)BannerVersionKind.Draft,
            AltText = "second draft",
            Weight = 1,
            SavedDate = Saved,
            SavedBy = Guid.NewGuid(),
        };

        Assert.That(() => db.Insert(duplicate), Throws.Exception);
    }

    [Test]
    public void CopyDraftOverLive_CopiesFieldsAndAssignments_InOneScope_AndPublishes()
    {
        Banner banner = NewBanner("Boat show");
        BannerZone zone;
        using (IScope scope = ScopeProvider.CreateScope())
        {
            zone = AddZone("zone-a");
            banner.Draft!.ZoneIds = [zone.Id];
            Banners.Add(banner);
            scope.Complete();
        }

        DateTime publishDate = Saved.AddDays(1);
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banners.CopyDraftOverLive(banner.Id, publishDate);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetByKey(banner.Key)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsPublished, Is.True);
                Assert.That(loaded.PublishDate, Is.EqualTo(publishDate));
                Assert.That(loaded.Live, Is.Not.Null);
                Assert.That(loaded.Live!.Kind, Is.EqualTo(BannerVersionKind.Live));
                Assert.That(loaded.Live.AltText, Is.EqualTo(loaded.Draft!.AltText));
                Assert.That(loaded.Live.LinkUrl, Is.EqualTo(loaded.Draft.LinkUrl));
                Assert.That(loaded.Live.MediaKey, Is.EqualTo(loaded.Draft.MediaKey));
                Assert.That(loaded.Live.Weight, Is.EqualTo(loaded.Draft.Weight));
                Assert.That(loaded.Live.ImpressionLimit, Is.EqualTo(1000));
                Assert.That(loaded.Live.ZoneIds, Is.EqualTo(new[] { zone.Id }));
                Assert.That(loaded.Live.Id, Is.Not.EqualTo(loaded.Draft.Id));
            });
        }
    }

    [Test]
    public void CopyDraftOverLive_Twice_ReplacesTheLiveVersionWithoutDuplicates()
    {
        Banner banner = NewBanner("Boat show");
        BannerZone zoneA, zoneB;
        using (IScope scope = ScopeProvider.CreateScope())
        {
            zoneA = AddZone("zone-a");
            zoneB = AddZone("zone-b");
            banner.Draft!.ZoneIds = [zoneA.Id];
            Banners.Add(banner);
            Banners.CopyDraftOverLive(banner.Id, Saved.AddDays(1));
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banner edit = Banners.GetById(banner.Id)!;
            edit.Draft!.AltText = "Second publish";
            edit.Draft.ZoneIds = [zoneA.Id, zoneB.Id];
            Banners.SaveDraft(edit);
            Banners.CopyDraftOverLive(banner.Id, Saved.AddDays(2));
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetById(banner.Id)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Live!.AltText, Is.EqualTo("Second publish"));
                Assert.That(loaded.Live.ZoneIds, Is.EquivalentTo(new[] { zoneA.Id, zoneB.Id }));
                Assert.That(loaded.PublishDate, Is.EqualTo(Saved.AddDays(2)));
            });
        }
    }

    [Test]
    public void SetPublished_False_KeepsTheLiveVersion()
    {
        Banner banner = NewBanner("Boat show");
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banners.Add(banner);
            Banners.CopyDraftOverLive(banner.Id, Saved.AddDays(1));
            Banners.SetPublished(banner.Id, false);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Banner loaded = Banners.GetById(banner.Id)!;
            Assert.Multiple(() =>
            {
                Assert.That(loaded.IsPublished, Is.False);
                Assert.That(loaded.Live, Is.Not.Null, "the live row stays so old click addresses still redirect");
            });
        }
    }

    [Test]
    public void Delete_CascadesToVersionsAndAssignments_ButKeepsDailyStats()
    {
        Banner banner = NewBanner("Boat show");
        BannerZone zone;
        using (IScope scope = ScopeProvider.CreateScope())
        {
            zone = AddZone("zone-a");
            banner.Draft!.ZoneIds = [zone.Id];
            Banners.Add(banner);
            Banners.CopyDraftOverLive(banner.Id, Saved.AddDays(1));
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new DailyStatDto
            {
                Day = new DateTime(2027, 1, 5),
                BannerId = banner.Id,
                ZoneId = zone.Id,
                Impressions = 12,
                Clicks = 3,
            });
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Banners.Delete(banner.Id);
            scope.Complete();
        }

        using IScope check = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = check.Database;
        Assert.Multiple(() =>
        {
            Assert.That(Banners.GetById(banner.Id), Is.Null);
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM meresoftBannersBannerVersion WHERE BannerId = @0", banner.Id), Is.EqualTo(0));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM meresoftBannersZoneAssignment"), Is.EqualTo(0));
            Assert.That(db.ExecuteScalar<int>("SELECT COUNT(*) FROM meresoftBannersDailyStat WHERE BannerId = @0", banner.Id), Is.EqualTo(1));
            Assert.That(Zones.GetById(zone.Id), Is.Not.Null, "deleting a banner never deletes a zone");
        });
    }

    [Test]
    public void GetPaged_FiltersByZoneTypeAndState_AndPages()
    {
        BannerZone zoneA, zoneB;
        Banner draft = NewBanner("Draft banner");
        Banner published = NewBanner("Published banner");
        Banner changed = NewBanner("Changed banner");
        Banner unpublished = NewBanner("Unpublished banner");
        Banner html = NewBanner("Html banner", BannerType.Html);
        html.Draft!.MediaKey = null;
        html.Draft.ClickLimit = null;

        using (IScope scope = ScopeProvider.CreateScope())
        {
            zoneA = AddZone("zone-a");
            zoneB = AddZone("zone-b");
            draft.Draft!.ZoneIds = [zoneA.Id];
            published.Draft!.ZoneIds = [zoneA.Id];
            changed.Draft!.ZoneIds = [zoneB.Id];
            unpublished.Draft!.ZoneIds = [zoneB.Id];
            html.Draft.ZoneIds = [zoneA.Id];
            foreach (Banner b in new[] { draft, published, changed, unpublished, html })
            {
                Banners.Add(b);
            }

            Banners.CopyDraftOverLive(published.Id, Saved.AddDays(1));

            Banners.CopyDraftOverLive(changed.Id, Saved.AddDays(1));
            Banner edit = Banners.GetById(changed.Id)!;
            edit.Draft!.SavedDate = Saved.AddDays(2);
            Banners.SaveDraft(edit);

            Banners.CopyDraftOverLive(unpublished.Id, Saved.AddDays(1));
            Banners.SetPublished(unpublished.Id, false);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            (IReadOnlyList<Banner> items, long total) All(BannerFilter f) => Banners.GetPaged(f);

            Assert.Multiple(() =>
            {
                Assert.That(All(new BannerFilter()).Total, Is.EqualTo(5));
                Assert.That(All(new BannerFilter { ZoneId = zoneA.Id }).Items.Select(b => b.Name),
                    Is.EquivalentTo(new[] { "Draft banner", "Published banner", "Html banner" }));
                Assert.That(All(new BannerFilter { Type = BannerType.Html }).Items.Select(b => b.Name), Is.EqualTo(new[] { "Html banner" }));
                Assert.That(All(new BannerFilter { State = BannerState.Published }).Items.Select(b => b.Name), Is.EqualTo(new[] { "Published banner" }));
                Assert.That(All(new BannerFilter { State = BannerState.PublishedWithChanges }).Items.Select(b => b.Name), Is.EqualTo(new[] { "Changed banner" }));
                Assert.That(All(new BannerFilter { State = BannerState.Unpublished }).Items.Select(b => b.Name), Is.EqualTo(new[] { "Unpublished banner" }));
                Assert.That(All(new BannerFilter { State = BannerState.Draft }).Items.Select(b => b.Name), Is.EquivalentTo(new[] { "Draft banner", "Html banner" }));

                (IReadOnlyList<Banner> page, long total) = All(new BannerFilter { Skip = 2, Take = 2 });
                Assert.That(total, Is.EqualTo(5));
                Assert.That(page, Has.Count.EqualTo(2));
            });
        }
    }
}
