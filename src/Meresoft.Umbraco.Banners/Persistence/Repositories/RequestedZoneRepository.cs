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
        RequestedZoneDto? existing = Database.FirstOrDefault<RequestedZoneDto>(
            NewSql().Select<RequestedZoneDto>().From<RequestedZoneDto>().Where<RequestedZoneDto>(r => r.Alias == alias));

        if (existing is null)
        {
            Database.Insert(new RequestedZoneDto { Alias = alias, FirstSeen = firstSeen, LastSeen = lastSeen });
        }
        else if (lastSeen > existing.LastSeen)
        {
            Database.Execute(NewSql()
                .Update<RequestedZoneDto>(u => u.Set(r => r.LastSeen, lastSeen))
                .Where<RequestedZoneDto>(r => r.Alias == alias));
        }
    }

    public bool Delete(string alias) =>
        Database.Execute(NewSql().Delete<RequestedZoneDto>().Where<RequestedZoneDto>(r => r.Alias == alias)) > 0;

    public IReadOnlyList<RequestedZone> GetAll() =>
        Database.Fetch<RequestedZoneDto>(NewSql().Select<RequestedZoneDto>().From<RequestedZoneDto>().OrderBy<RequestedZoneDto>(r => r.FirstSeen))
            .Select(d => new RequestedZone(d.Alias, Utc(d.FirstSeen), Utc(d.LastSeen)))
            .ToList();
}
