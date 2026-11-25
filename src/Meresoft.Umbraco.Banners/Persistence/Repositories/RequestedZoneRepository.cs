using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <inheritdoc cref="IRequestedZoneRepository" />
internal sealed class RequestedZoneRepository : RepositoryBase, IRequestedZoneRepository
{
    public RequestedZoneRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public void Upsert(string alias, DateTime firstSeenUtc, DateTime lastSeenUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(alias);

        DateTime firstSeen = UtcTimestamp.Truncate(firstSeenUtc);
        DateTime lastSeen = UtcTimestamp.Truncate(lastSeenUtc);
        // Writes first and never reads before it: a read then a write in one transaction holds a read lock while asking for the
        // write lock, which can wait for another connection of the process that does the same (SQLite shared cache).
        // Move the last seen time forward, and insert only when no row exists, in one statement.
        Database.Execute(NewSql()
            .Update<RequestedZoneDto>(u => u.Set(r => r.LastSeen, lastSeen))
            .Where<RequestedZoneDto>(r => r.Alias == alias && r.LastSeen < lastSeen));

        string table = Syntax.GetQuotedTableName(RequestedZoneDto.TableName);
        string aliasColumn = Syntax.GetQuotedColumnName("Alias");
        Database.Execute(
            $"INSERT INTO {table} ({aliasColumn}, {Syntax.GetQuotedColumnName("FirstSeen")}, {Syntax.GetQuotedColumnName("LastSeen")}) "
            + $"SELECT @0, @1, @2 WHERE NOT EXISTS (SELECT 1 FROM {table} WHERE {aliasColumn} = @0)",
            alias,
            firstSeen,
            lastSeen);
    }

    public bool Delete(string alias) =>
        Database.Execute(NewSql().Delete<RequestedZoneDto>().Where<RequestedZoneDto>(r => r.Alias == alias)) > 0;

    public IReadOnlyList<RequestedZone> GetAll() =>
        Database.Fetch<RequestedZoneDto>(NewSql().Select<RequestedZoneDto>().From<RequestedZoneDto>().OrderBy<RequestedZoneDto>(r => r.FirstSeen))
            .Select(d => new RequestedZone(d.Alias, Utc(d.FirstSeen), Utc(d.LastSeen)))
            .ToList();
}
