using NPoco;

namespace Meresoft.Umbraco.Banners.Persistence.Dtos;

/// <summary>
/// Read-only NPoco mapping of Umbraco's own audit table. Umbraco's mapping class is internal, and the package only
/// reads the rows it wrote itself (entity types meresoft-banner and meresoft-banner-zone).
/// </summary>
[TableName(TableName)]
[PrimaryKey("id")]
[ExplicitColumns]
internal sealed class AuditLogRowDto
{
    public const string TableName = "umbracoLog";

    [Column("id")]
    public int Id { get; set; }

    [Column("userId")]
    public int UserId { get; set; }

    [Column("NodeId")]
    public int NodeId { get; set; }

    [Column("entityType")]
    public string? EntityType { get; set; }

    [Column("Datestamp")]
    public DateTime Datestamp { get; set; }

    [Column("logHeader")]
    public string Header { get; set; } = string.Empty;
}
