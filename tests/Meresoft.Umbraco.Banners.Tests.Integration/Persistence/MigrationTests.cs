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

    private bool IsSqlite()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return scope.Database.DatabaseType.GetProviderName().Contains("sqlite", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Primary key columns of a table, read from the catalog of the database in use.</summary>
    private List<string> PrimaryKeyColumns(string table)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        return IsSqlite()
            ? db.Fetch<string>("SELECT name FROM pragma_table_info(@0) WHERE pk > 0", table)
            : db.Fetch<string>(
                "SELECT k.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS t " +
                "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k ON k.CONSTRAINT_NAME = t.CONSTRAINT_NAME " +
                "WHERE t.CONSTRAINT_TYPE = 'PRIMARY KEY' AND t.TABLE_NAME = @0",
                table);
    }

    /// <summary>Foreign keys of a table: referenced table and delete rule (CASCADE or NO ACTION).</summary>
    private List<(string Referenced, string OnDelete)> ForeignKeys(string table)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        if (IsSqlite())
        {
            return db.Fetch<dynamic>("SELECT \"table\" AS referenced, on_delete AS ondelete FROM pragma_foreign_key_list(@0)", table)
                .Select(r => ((string)r.referenced, ((string)r.ondelete).ToUpperInvariant()))
                .ToList();
        }

        return db.Fetch<dynamic>(
                "SELECT OBJECT_NAME(referenced_object_id) AS referenced, delete_referential_action_desc AS ondelete " +
                "FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(@0)",
                table)
            .Select(r => ((string)r.referenced, ((string)r.ondelete).Replace('_', ' ').ToUpperInvariant()))
            .ToList();
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

        Assert.Multiple(() =>
        {
            Assert.That(PrimaryKeyColumns("meresoftBannersZone"), Is.EquivalentTo(new[] { "Id" }));
            Assert.That(PrimaryKeyColumns("meresoftBannersBanner"), Is.EquivalentTo(new[] { "Id" }));
            Assert.That(PrimaryKeyColumns("meresoftBannersBannerVersion"), Is.EquivalentTo(new[] { "Id" }));
            Assert.That(PrimaryKeyColumns("meresoftBannersZoneAssignment"), Is.EquivalentTo(new[] { "BannerVersionId", "ZoneId" }));
            Assert.That(PrimaryKeyColumns("meresoftBannersDailyStat"), Is.EquivalentTo(new[] { "Day", "BannerId", "ZoneId" }));
            Assert.That(PrimaryKeyColumns("meresoftBannersRequestedZone"), Is.EquivalentTo(new[] { "Alias" }));
        });
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
        Assert.That(ForeignKeys("meresoftBannersDailyStat"), Is.Empty, "DailyStat rows must survive banner and zone deletion");
    }

    [Test]
    public async Task FreshInstall_CreatesTheForeignKeys_WithTheRightDeleteRules()
    {
        await InstallPackageAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                ForeignKeys("meresoftBannersBannerVersion").Select(f => (f.Referenced.ToLowerInvariant(), f.OnDelete)),
                Is.EquivalentTo(new[] { ("meresoftbannersbanner", "CASCADE") }));
            Assert.That(
                ForeignKeys("meresoftBannersZoneAssignment").Select(f => (f.Referenced.ToLowerInvariant(), f.OnDelete)),
                Is.EquivalentTo(new[] { ("meresoftbannersbannerversion", "CASCADE"), ("meresoftbannerszone", "NO ACTION") }));
        });
    }

    [Test]
    public async Task DailyStat_Day_IsADateWithoutTimePart()
    {
        await InstallPackageAsync();

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase db = scope.Database;
        db.Insert(new Meresoft.Umbraco.Banners.Persistence.Dtos.DailyStatDto
        {
            Day = new DateOnly(2027, 1, 5),
            BannerId = 1,
            ZoneId = 1,
            Impressions = 1,
        });

        string stored = db.ExecuteScalar<string>("SELECT CAST(\"Day\" AS VARCHAR(30)) FROM meresoftBannersDailyStat");
        Assert.That(stored, Is.EqualTo("2027-01-05"), "no time part is stored");
        if (!IsSqlite())
        {
            // SQL Server has a real date type; SQLite stores dates as text.
            Assert.That(
                db.ExecuteScalar<string>("SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'meresoftBannersDailyStat' AND COLUMN_NAME = 'Day'"),
                Is.EqualTo("date"));
        }
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
