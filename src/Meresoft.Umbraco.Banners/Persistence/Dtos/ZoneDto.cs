using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>Runtime NPoco mapping of the table (the schema is frozen in the migration namespace).</summary>
[TableName(TableName)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class ZoneDto
{
    public const string TableName = "meresoftBannersZone";

    [Column("Id")]
    public int Id { get; set; }

    [Column("Key")]
    public Guid Key { get; set; }

    [Column("Alias")]
    public string Alias { get; set; } = string.Empty;

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Description")]
    public string? Description { get; set; }

    [Column("ShowCount")]
    public int ShowCount { get; set; }

    [Column("ImageWidth")]
    public int? ImageWidth { get; set; }

    [Column("ImageHeight")]
    public int? ImageHeight { get; set; }

    [Column("RenderMode")]
    public byte RenderMode { get; set; }

    [Column("CreateDate")]
    public DateTime CreateDate { get; set; }

    [Column("UpdateDate")]
    public DateTime UpdateDate { get; set; }
}
