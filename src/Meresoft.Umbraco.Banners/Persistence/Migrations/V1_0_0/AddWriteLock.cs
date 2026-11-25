using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.V1_0_0;

/// <summary>
/// Adds the package's own row to the Umbraco lock table, so a scope can take <see cref="BannersConstants.WriteLockId"/>.
/// Only a row is added; nothing of Umbraco is changed or removed. Uninstalling the package removes the row with the
/// documented table removal step. Running the step again adds nothing.
/// </summary>
public sealed class AddWriteLock : AsyncMigrationBase
{
    private const string LockName = "Meresoft.Banners.Write";

    /// <summary>Creates the step.</summary>
    public AddWriteLock(IMigrationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override Task MigrateAsync()
    {
        string table = SqlSyntax.GetQuotedTableName(Constants.DatabaseSchema.Tables.Lock);
        string id = SqlSyntax.GetQuotedColumnName("id");
        Database.Execute(
            $"INSERT INTO {table} ({id}, {SqlSyntax.GetQuotedColumnName("value")}, {SqlSyntax.GetQuotedColumnName("name")}) "
            + $"SELECT @0, 1, @1 WHERE NOT EXISTS (SELECT 1 FROM {table} WHERE {id} = @0)",
            BannersConstants.WriteLockId,
            LockName);
        return Task.CompletedTask;
    }
}
