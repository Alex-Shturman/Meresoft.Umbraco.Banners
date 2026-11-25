using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>Frozen schema of the zone table, version 1. Never edit; later versions add new migration steps.</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class ZoneSchemaDto
{
    public const string TableName = "meresoftBannersZone";

    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("Key")]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_meresoftBannersZone_Key")]
    public Guid Key { get; set; }

    [Column("Alias")]
    [Length(100)]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_meresoftBannersZone_Alias")]
    public string Alias { get; set; } = string.Empty;

    [Column("Name")]
    [Length(255)]
    public string Name { get; set; } = string.Empty;

    [Column("Description")]
    [Length(1000)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Description { get; set; }

    [Column("ShowCount")]
    public int ShowCount { get; set; }

    [Column("ImageWidth")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? ImageWidth { get; set; }

    [Column("ImageHeight")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public int? ImageHeight { get; set; }

    [Column("RenderMode")]
    public byte RenderMode { get; set; }

    [Column("CreateDate")]
    public DateTime CreateDate { get; set; }

    [Column("UpdateDate")]
    public DateTime UpdateDate { get; set; }
}
