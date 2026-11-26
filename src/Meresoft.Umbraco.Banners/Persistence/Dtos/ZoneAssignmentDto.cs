using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("BannerVersionId,ZoneId", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class ZoneAssignmentDto
{
    public const string TableName = "meresoftBannersZoneAssignment";

    [Column("BannerVersionId")]
    public int BannerVersionId { get; set; }

    [Column("ZoneId")]
    public int ZoneId { get; set; }
}
