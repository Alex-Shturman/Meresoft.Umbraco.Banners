using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Persistence;

[TestFixture]
public class MigrationTests : BannersIntegrationTestBase
{
    private static readonly string[] BannerTables =
    [
        "meresoftBannersZone",
        "meresoftBannersBanner",
        "meresoftBannersBannerVersion",
        "meresoftBannersZoneAssignment",
        "meresoftBannersDailyStat",
        "meresoftBannersRequestedZone",
    ];

    private HashSet<string> Tables()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        return db.SqlContext.SqlSyntax.GetTablesInSchema(db).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private List<(string Table, string Index, string Column, bool Unique)> Indexes()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        return db.SqlContext.SqlSyntax.GetDefinedIndexes(db).Select(i => (i.Item1, i.Item2, i.Item3, i.Item4)).ToList();
    }

    private List<(string Table, string Constraint)> Constraints()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        return db.SqlContext.SqlSyntax.GetConstraintsPerTable(db).Select(c => (c.Item1, c.Item2)).ToList();
    }

    private string CoreSchemaSignature()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        IEnumerable<string> columns = db.SqlContext.SqlSyntax.GetColumnsInSchema(db)
            .Where(c => !c.TableName.StartsWith("meresoftBanners", StringComparison.OrdinalIgnoreCase))
            .Select(c => $"{c.TableName}.{c.ColumnName}:{c.DataType}:{c.IsNullable}")
            .OrderBy(x => x, StringComparer.Ordinal);
        return string.Join("|", columns);
    }

    [Test]
    public async Task FreshInstall_CreatesTheSixTables()
    {
        Assert.That(Tables().Intersect(BannerTables, StringComparer.OrdinalIgnoreCase), Is.Empty, "tables must not exist before the plan runs");

        await InstallPackageAsync();

        Assert.That(Tables(), Is.SupersetOf(BannerTables).IgnoreCase);
    }

    [Test]
    public async Task FreshInstall_CreatesPrimaryKeys()
    {
        await InstallPackageAsync();

        List<(string Table, string Constraint)> constraints = Constraints();
        foreach (string table in BannerTables)
        {
            Assert.That(
                constraints.Any(c => string.Equals(c.Table, table, StringComparison.OrdinalIgnoreCase) && c.Constraint.StartsWith("PK_", StringComparison.OrdinalIgnoreCase)),
                Is.True,
                $"primary key missing on {table}");
        }
    }

    [Test]
    public async Task FreshInstall_CreatesTheUniqueIndexes()
    {
        await InstallPackageAsync();

        List<(string Table, string Index, string Column, bool Unique)> indexes = Indexes();

        bool UniqueOn(string table, params string[] columns)
        {
            return indexes
                .Where(i => i.Unique && string.Equals(i.Table, table, StringComparison.OrdinalIgnoreCase))
                .GroupBy(i => i.Index)
                .Any(g => g.Select(i => i.Column).OrderBy(c => c).SequenceEqual(columns.OrderBy(c => c), StringComparer.OrdinalIgnoreCase));
        }

        Assert.Multiple(() =>
        {
            Assert.That(UniqueOn("meresoftBannersZone", "Alias"), Is.True, "Zone.Alias");
            Assert.That(UniqueOn("meresoftBannersZone", "Key"), Is.True, "Zone.Key");
            Assert.That(UniqueOn("meresoftBannersBanner", "Key"), Is.True, "Banner.Key");
            Assert.That(UniqueOn("meresoftBannersBannerVersion", "BannerId", "Kind"), Is.True, "BannerVersion (BannerId, Kind)");
        });
    }

    [Test]
    public async Task FreshInstall_CreatesTheDailyStatIndexAndNoForeignKeyOnIt()
    {
        await InstallPackageAsync();

        List<(string Table, string Index, string Column, bool Unique)> indexes = Indexes();
        bool hasBannerDayIndex = indexes
            .Where(i => string.Equals(i.Table, "meresoftBannersDailyStat", StringComparison.OrdinalIgnoreCase))
            .GroupBy(i => i.Index)
            .Any(g => g.Select(i => i.Column).SequenceEqual(["BannerId", "Day"], StringComparer.OrdinalIgnoreCase)
                      || g.Select(i => i.Column).OrderBy(c => c).SequenceEqual(["BannerId", "Day"], StringComparer.OrdinalIgnoreCase));

        Assert.That(hasBannerDayIndex, Is.True, "index (BannerId, Day)");
        Assert.That(
            Constraints().Where(c => string.Equals(c.Table, "meresoftBannersDailyStat", StringComparison.OrdinalIgnoreCase)),
            Has.None.Matches<(string Table, string Constraint)>(c => c.Constraint.StartsWith("FK_", StringComparison.OrdinalIgnoreCase)),
            "DailyStat rows must survive banner and zone deletion");
    }

    [Test]
    public async Task FreshInstall_CreatesTheForeignKeys()
    {
        await InstallPackageAsync();

        List<(string Table, string Constraint)> constraints = Constraints();

        Assert.Multiple(() =>
        {
            Assert.That(constraints.Count(c => c.Table.Equals("meresoftBannersBannerVersion", StringComparison.OrdinalIgnoreCase) && c.Constraint.StartsWith("FK_", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
            Assert.That(constraints.Count(c => c.Table.Equals("meresoftBannersZoneAssignment", StringComparison.OrdinalIgnoreCase) && c.Constraint.StartsWith("FK_", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RunningThePlanAgain_ChangesNothing()
    {
        await InstallPackageAsync();
        HashSet<string> tablesAfterFirstRun = Tables();
        string coreAfterFirstRun = CoreSchemaSignature();

        await InstallPackageAsync();

        Assert.Multiple(() =>
        {
            Assert.That(Tables(), Is.EquivalentTo(tablesAfterFirstRun));
            Assert.That(CoreSchemaSignature(), Is.EqualTo(coreAfterFirstRun));
        });
    }

    [Test]
    public async Task Install_DoesNotChangeAnyUmbracoCoreTable()
    {
        string before = CoreSchemaSignature();
        HashSet<string> coreTablesBefore = Tables();

        await InstallPackageAsync();

        Assert.Multiple(() =>
        {
            Assert.That(CoreSchemaSignature(), Is.EqualTo(before), "columns of core tables changed");
            Assert.That(
                Tables().Where(t => !t.StartsWith("meresoftBanners", StringComparison.OrdinalIgnoreCase)),
                Is.EquivalentTo(coreTablesBefore),
                "core tables were added or removed");
        });
    }
}
