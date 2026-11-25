using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>One row of Umbraco's audit log (the <c>umbracoLog</c> table).</summary>
/// <param name="UserId">Internal id of the user.</param>
/// <param name="Action">The audit type as Umbraco stores it (for example "Save").</param>
/// <param name="CreatedUtc">When the change was written, UTC.</param>
internal sealed record AuditHistoryRow(int UserId, string Action, DateTime CreatedUtc);

/// <summary>Reads the audit rows of one banner or zone. Methods use the ambient Umbraco scope.</summary>
internal interface IAuditHistoryRepository
{
    /// <summary>A page of the rows of the entity, newest first, and how many rows the entity has in all.</summary>
    (long Total, IReadOnlyList<AuditHistoryRow> Rows) GetPage(int entityId, string entityType, int skip, int take);
}

/// <inheritdoc cref="IAuditHistoryRepository" />
/// <remarks>
/// Umbraco's audit service filters by entity id only, and banners, zones and content nodes share the id space, so
/// the entity type is part of the query here. The database filters, orders (newest first, then by id, so equal
/// timestamps never reorder between pages) and pages, and nothing is written.
/// </remarks>
internal sealed class AuditHistoryRepository : RepositoryBase, IAuditHistoryRepository
{
    public AuditHistoryRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public (long Total, IReadOnlyList<AuditHistoryRow> Rows) GetPage(int entityId, string entityType, int skip, int take)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityType);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        long total = Database.ExecuteScalar<long>(
            NewSql().SelectCount().From<AuditLogRowDto>().Where<AuditLogRowDto>(l => l.NodeId == entityId && l.EntityType == entityType));

        List<AuditLogRowDto> rows = Database.SkipTake<AuditLogRowDto>(
            skip,
            take,
            NewSql()
                .Select<AuditLogRowDto>()
                .From<AuditLogRowDto>()
                .Where<AuditLogRowDto>(l => l.NodeId == entityId && l.EntityType == entityType)
                .OrderByDescending<AuditLogRowDto>(l => l.Datestamp)
                .OrderByDescending<AuditLogRowDto>(l => l.Id));

        return (total, rows.Select(r => new AuditHistoryRow(r.UserId, r.Header, Utc(r.Datestamp))).ToList());
    }
}
