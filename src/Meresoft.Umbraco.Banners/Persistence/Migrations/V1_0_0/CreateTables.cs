using Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.V1_0_0;

/// <summary>Creates the six tables of the package, in dependency order. Each is created only when missing.</summary>
public sealed class CreateTables : AsyncMigrationBase
{
    /// <summary>Creates the step.</summary>
    public CreateTables(IMigrationContext context)
        : base(context)
    {
    }

    /// <inheritdoc />
    protected override Task MigrateAsync()
    {
        CreateIfMissing<ZoneSchemaDto>(ZoneSchemaDto.TableName);
        CreateIfMissing<BannerSchemaDto>(BannerSchemaDto.TableName);
        CreateIfMissing<BannerVersionSchemaDto>(BannerVersionSchemaDto.TableName);
        CreateIfMissing<ZoneAssignmentSchemaDto>(ZoneAssignmentSchemaDto.TableName);
        CreateIfMissing<DailyStatSchemaDto>(DailyStatSchemaDto.TableName);
        CreateIfMissing<RequestedZoneSchemaDto>(RequestedZoneSchemaDto.TableName);
        return Task.CompletedTask;
    }

    private void CreateIfMissing<TDto>(string tableName)
    {
        if (!TableExists(tableName))
        {
            Create.Table<TDto>().Do();
        }
    }
}
