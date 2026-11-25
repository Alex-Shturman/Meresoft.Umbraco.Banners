using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Meresoft.Umbraco.Banners.Persistence.Migrations.Schema.V1;

/// <summary>
/// Frozen schema of the daily statistics table, version 1. No foreign keys: rows survive the
/// deletion of banners and zones. The day is the UTC day, stored as midnight.
/// </summary>
[TableName(TableName)]
[PrimaryKey("Day,BannerId,ZoneId", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class DailyStatSchemaDto
{
    public const string TableName = "meresoftBannersDailyStat";

    [Column("Day")]
    [PrimaryKeyColumn(AutoIncrement = false, Name = "PK_meresoftBannersDailyStat", OnColumns = "Day, BannerId, ZoneId")]
    public DateTime Day { get; set; }

    [Column("BannerId")]
    [Index(IndexTypes.NonClustered, ForColumns = "BannerId,Day", Name = "IX_meresoftBannersDailyStat_BannerId_Day")]
    public int BannerId { get; set; }

    [Column("ZoneId")]
    public int ZoneId { get; set; }

    [Column("Impressions")]
    public long Impressions { get; set; }

    [Column("Clicks")]
    public long Clicks { get; set; }

    [Column("IsImported")]
    public bool IsImported { get; set; }
}
