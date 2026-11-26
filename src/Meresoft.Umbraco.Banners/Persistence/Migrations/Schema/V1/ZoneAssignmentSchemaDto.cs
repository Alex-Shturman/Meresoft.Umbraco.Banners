using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>
/// Frozen schema of the zone assignment table, version 1. Cascades from the version, but not from
/// the zone: a zone that still has assignments cannot be deleted.
/// </summary>
[TableName(TableName)]
[PrimaryKey("BannerVersionId,ZoneId", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class ZoneAssignmentSchemaDto
{
    public const string TableName = "meresoftBannersZoneAssignment";

    [Column("BannerVersionId")]
    [PrimaryKeyColumn(AutoIncrement = false, Name = "PK_meresoftBannersZoneAssignment", OnColumns = "BannerVersionId, ZoneId")]
    [ForeignKey(typeof(BannerVersionSchemaDto), Column = "Id", Name = "FK_meresoftBannersZoneAssignment_BannerVersion", OnDelete = System.Data.Rule.Cascade)]
    public int BannerVersionId { get; set; }

    [Column("ZoneId")]
    [ForeignKey(typeof(ZoneSchemaDto), Column = "Id", Name = "FK_meresoftBannersZoneAssignment_Zone")]
    public int ZoneId { get; set; }
}
