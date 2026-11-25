using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>Frozen schema of the requested zone table, version 1.</summary>
[TableName(TableName)]
[PrimaryKey("Alias", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class RequestedZoneSchemaDto
{
    public const string TableName = "meresoftBannersRequestedZone";

    [Column("Alias")]
    [PrimaryKeyColumn(AutoIncrement = false, Name = "PK_meresoftBannersRequestedZone")]
    [Length(100)]
    public string Alias { get; set; } = string.Empty;

    [Column("FirstSeen")]
    public DateTime FirstSeen { get; set; }

    [Column("LastSeen")]
    public DateTime LastSeen { get; set; }
}
