using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Persistence;

[TestFixture]
public class RequestedZoneRepositoryTests : BannersIntegrationTestBase
{
    private static readonly DateTime T0 = new(2027, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private IRequestedZoneRepository Requested => GetRequiredService<IRequestedZoneRepository>();

    [SetUp]
    public async Task InstallSchema() => await InstallPackageAsync();

    [Test]
    public void Upsert_InsertsThenKeepsFirstSeenAndMovesLastSeen()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Requested.Upsert("home-top", T0, T0);
            Requested.Upsert("home-top", T0.AddHours(2), T0.AddHours(3));
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            RequestedZone row = Requested.GetAll().Single();
            Assert.Multiple(() =>
            {
                Assert.That(row.Alias, Is.EqualTo("home-top"));
                Assert.That(row.FirstSeen, Is.EqualTo(T0));
                Assert.That(row.LastSeen, Is.EqualTo(T0.AddHours(3)));
            });
        }
    }

    [Test]
    public void Upsert_DoesNotMoveLastSeenBackwards()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Requested.Upsert("home-top", T0, T0.AddHours(5));
            Requested.Upsert("home-top", T0, T0.AddHours(1));
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Requested.GetAll().Single().LastSeen, Is.EqualTo(T0.AddHours(5)));
        }
    }

    [Test]
    public void Delete_RemovesOnlyTheAlias_AndReportsWhetherARowWasRemoved()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            Requested.Upsert("one", T0, T0);
            Requested.Upsert("two", T0, T0);
            scope.Complete();
        }

        using (IScope scope = ScopeProvider.CreateScope())
        {
            Assert.That(Requested.Delete("one"), Is.True);
            Assert.That(Requested.Delete("missing"), Is.False);
            scope.Complete();
        }

        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(Requested.GetAll().Select(r => r.Alias), Is.EqualTo(new[] { "two" }));
        }
    }
}
