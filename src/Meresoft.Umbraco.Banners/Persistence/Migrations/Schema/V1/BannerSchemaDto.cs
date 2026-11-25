using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>Frozen schema of the banner table, version 1.</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class BannerSchemaDto
{
    public const string TableName = "meresoftBannersBanner";

    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("Key")]
    [Index(IndexTypes.UniqueNonClustered, Name = "IX_meresoftBannersBanner_Key")]
    public Guid Key { get; set; }

    [Column("Name")]
    [Length(255)]
    public string Name { get; set; } = string.Empty;

    [Column("Type")]
    public byte Type { get; set; }

    [Column("IsPublished")]
    public bool IsPublished { get; set; }

    [Column("CreateDate")]
    public DateTime CreateDate { get; set; }

    [Column("UpdateDate")]
    public DateTime UpdateDate { get; set; }

    [Column("CreatedBy")]
    public Guid CreatedBy { get; set; }

    [Column("UpdatedBy")]
    public Guid UpdatedBy { get; set; }

    [Column("PublishDate")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? PublishDate { get; set; }
}
