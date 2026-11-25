using NPoco;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <inheritdoc cref="IZoneRepository" />
internal sealed class ZoneRepository : RepositoryBase, IZoneRepository
{
    public ZoneRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public BannerZone Add(BannerZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ZoneDto dto = BannerMapper.ToDto(zone);
        Database.Insert(dto);
        zone.Id = dto.Id;
        return zone;
    }

    public void Update(BannerZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        RequireRow(Database.Update(BannerMapper.ToDto(zone)), "Zone", zone.Id);
    }

    public void Delete(int id) => Database.Delete<ZoneDto>(id);

    public BannerZone? GetByKey(Guid key) =>
        BannerMapper.Map(Database.FirstOrDefault<ZoneDto>(Select().Where<ZoneDto>(z => z.Key == key)));

    public BannerZone? GetByAlias(string alias) =>
        BannerMapper.Map(Database.FirstOrDefault<ZoneDto>(Select().Where<ZoneDto>(z => z.Alias == alias)));

    public BannerZone? GetById(int id) =>
        BannerMapper.Map(Database.FirstOrDefault<ZoneDto>(Select().Where<ZoneDto>(z => z.Id == id)));

    public IReadOnlyList<BannerZone> GetAll() =>
        Database.Fetch<ZoneDto>(Select().OrderBy<ZoneDto>(z => z.Name).OrderBy<ZoneDto>(z => z.Alias))
            .Select(d => BannerMapper.Map(d)!)
            .ToList();

    public IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId)
    {
        Sql<ISqlContext> sql = NewSql()
            .Select<BannerDto>(b => b.Key)
            .From<BannerDto>()
            .InnerJoin<BannerVersionDto>().On<BannerDto, BannerVersionDto>((b, v) => b.Id == v.BannerId)
            .InnerJoin<ZoneAssignmentDto>().On<BannerVersionDto, ZoneAssignmentDto>((v, a) => v.Id == a.BannerVersionId)
            .Where<ZoneAssignmentDto>(a => a.ZoneId == zoneId);

        // A banner can be assigned through both its draft and its live version.
        return Database.Fetch<Guid>(sql).Distinct().ToList();
    }

    private Sql<ISqlContext> Select() => NewSql().Select<ZoneDto>().From<ZoneDto>();
}
