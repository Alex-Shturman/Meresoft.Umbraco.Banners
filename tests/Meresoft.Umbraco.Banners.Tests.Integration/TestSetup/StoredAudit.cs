using Meresoft.Umbraco.Banners.Persistence;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>One row of the audit table as the package stored it.</summary>
internal sealed record StoredAuditRow(int UserId, string EntityType, AuditType AuditType, string Comment);

/// <summary>
/// Reads the audit rows of a banner or zone straight from the table. Umbraco's own audit service reads only
/// positive node ids, and the rows of the package use the negative ids of <see cref="AuditNodeId"/>.
/// </summary>
internal static class StoredAudit
{
    public static StoredAuditRow[] For(IScopeProvider scopeProvider, int entityId, string entityType)
    {
        using IScope scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database
            .Fetch<AuditRowDto>(
                "SELECT userId, entityType, logHeader, logComment FROM umbracoLog WHERE NodeId = @0 AND entityType = @1 ORDER BY id",
                AuditNodeId.FromEntityId(entityId),
                entityType)
            .Select(r => new StoredAuditRow(r.UserId, r.EntityType, Enum.Parse<AuditType>(r.LogHeader), r.LogComment))
            .ToArray();
    }

    /// <summary>The number of audit rows (of any entity type) with exactly this node id.</summary>
    public static int CountAtNode(IScopeProvider scopeProvider, int nodeId)
    {
        using IScope scope = scopeProvider.CreateScope(autoComplete: true);
        return scope.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoLog WHERE NodeId = @0", nodeId);
    }

    private sealed class AuditRowDto
    {
        public int UserId { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public string LogHeader { get; set; } = string.Empty;

        public string LogComment { get; set; } = string.Empty;
    }
}
