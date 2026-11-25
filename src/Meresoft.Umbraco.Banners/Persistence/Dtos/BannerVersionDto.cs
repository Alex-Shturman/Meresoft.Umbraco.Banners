using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class BannerVersionDto
{
    public const string TableName = "meresoftBannersBannerVersion";

    [Column("Id")]
    public int Id { get; set; }

    [Column("BannerId")]
    public int BannerId { get; set; }

    [Column("Kind")]
    public byte Kind { get; set; }

    [Column("MediaKey")]
    public Guid? MediaKey { get; set; }

    [Column("Html")]
    public string? Html { get; set; }

    [Column("LinkUrl")]
    public string? LinkUrl { get; set; }

    [Column("OpenInNewWindow")]
    public bool OpenInNewWindow { get; set; }

    [Column("AltText")]
    public string AltText { get; set; } = string.Empty;

    [Column("Title")]
    public string? Title { get; set; }

    [Column("Weight")]
    public int Weight { get; set; }

    [Column("StartUtc")]
    public DateTime? StartUtc { get; set; }

    [Column("EndUtc")]
    public DateTime? EndUtc { get; set; }

    [Column("ImpressionLimit")]
    public long? ImpressionLimit { get; set; }

    [Column("ClickLimit")]
    public long? ClickLimit { get; set; }

    [Column("SavedDate")]
    public DateTime SavedDate { get; set; }

    [Column("SavedBy")]
    public Guid SavedBy { get; set; }
}
