using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("Day,BannerId,ZoneId", AutoIncrement = false)]
[ExplicitColumns]
internal sealed class DailyStatDto
{
    public const string TableName = "meresoftBannersDailyStat";

    [Column("Day")]
    public DateOnly Day { get; set; }

    [Column("BannerId")]
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
