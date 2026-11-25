using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>Frozen schema of the banner version table, version 1. At most one Draft and one Live row per banner.</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class BannerVersionSchemaDto
{
    public const string TableName = "meresoftBannersBannerVersion";

    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("BannerId")]
    [ForeignKey(typeof(BannerSchemaDto), Column = "Id", Name = "FK_meresoftBannersBannerVersion_Banner", OnDelete = System.Data.Rule.Cascade)]
    [Index(IndexTypes.UniqueNonClustered, ForColumns = "BannerId,Kind", Name = "IX_meresoftBannersBannerVersion_BannerId_Kind")]
    public int BannerId { get; set; }

    [Column("Kind")]
    public byte Kind { get; set; }

    [Column("MediaKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public Guid? MediaKey { get; set; }

    [Column("Html")]
    [SpecialDbType(SpecialDbTypes.NVARCHARMAX)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Html { get; set; }

    [Column("LinkUrl")]
    [Length(2048)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? LinkUrl { get; set; }

    [Column("OpenInNewWindow")]
    public bool OpenInNewWindow { get; set; }

    [Column("AltText")]
    [Length(255)]
    public string AltText { get; set; } = string.Empty;

    [Column("Title")]
    [Length(255)]
    [NullSetting(NullSetting = NullSettings.Null)]
    public string? Title { get; set; }

    [Column("Weight")]
    public int Weight { get; set; }

    [Column("StartUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? StartUtc { get; set; }

    [Column("EndUtc")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public DateTime? EndUtc { get; set; }

    [Column("ImpressionLimit")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public long? ImpressionLimit { get; set; }

    [Column("ClickLimit")]
    [NullSetting(NullSetting = NullSettings.Null)]
    public long? ClickLimit { get; set; }

    [Column("SavedDate")]
    public DateTime SavedDate { get; set; }

    [Column("SavedBy")]
    public Guid SavedBy { get; set; }
}
