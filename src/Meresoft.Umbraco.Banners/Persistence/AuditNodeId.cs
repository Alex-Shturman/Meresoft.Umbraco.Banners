namespace Meresoft.Umbraco.Banners.Persistence;

/// <summary>
/// Maps a banner or zone id to the <c>NodeId</c> of its <c>umbracoLog</c> row. Umbraco reads the history of a document
/// or media item by node id alone, so a banner id equal to a node id would show banner rows in that node's history.
/// Package rows therefore use ids below every node id Umbraco uses. The column is a plain signed integer with an
/// index and no foreign key (the audit table mapping of Umbraco 17), so negative values are valid on SQLite and SQL
/// Server. The writer and the history repository both use this one mapping.
/// </summary>
internal static class AuditNodeId
{
    /// <summary>The offset: package rows are stored as <c>Offset - id</c>. System nodes are above -1,000,000.</summary>
    internal const int Offset = -1_000_000_000;

    /// <summary>The audit <c>NodeId</c> of the banner or zone with this id.</summary>
    public static int FromEntityId(int entityId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(entityId, 1);
        return Offset - entityId;
    }
}
