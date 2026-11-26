using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("Alias", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class RequestedZoneDto
{
    public const string TableName = "meresoftBannersRequestedZone";

    [Column("Alias")]
    public string Alias { get; set; } = string.Empty;

    [Column("FirstSeen")]
    public DateTime FirstSeen { get; set; }

    [Column("LastSeen")]
    public DateTime LastSeen { get; set; }
}
