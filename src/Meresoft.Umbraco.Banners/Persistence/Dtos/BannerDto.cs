using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class BannerDto
{
    public const string TableName = "meresoftBannersBanner";

    [Column("Id")]
    public int Id { get; set; }

    [Column("Key")]
    public Guid Key { get; set; }

    [Column("Name")]
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
    public DateTime? PublishDate { get; set; }
}
