using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;

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
        ZoneDto dto = ToDto(zone);
        Database.Insert(dto);
        zone.Id = dto.Id;
        return zone;
    }

    public void Update(BannerZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        Database.Update(ToDto(zone));
    }

    public void Delete(int id) => Database.Delete<ZoneDto>(id);

    public BannerZone? GetByKey(Guid key) =>
        Map(Database.FirstOrDefault<ZoneDto>("WHERE \"Key\" = @0", key));

    public BannerZone? GetByAlias(string alias) =>
        Map(Database.FirstOrDefault<ZoneDto>("WHERE \"Alias\" = @0", alias));

    public BannerZone? GetById(int id) =>
        Map(Database.FirstOrDefault<ZoneDto>("WHERE \"Id\" = @0", id));

    public IReadOnlyList<BannerZone> GetAll() =>
        Database.Fetch<ZoneDto>("ORDER BY \"Name\", \"Alias\"").Select(d => Map(d)!).ToList();

    public IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId) =>
        Database.Fetch<Guid>(
            $"SELECT DISTINCT b.\"Key\" FROM {BannerDto.TableName} b " +
            $"INNER JOIN {BannerVersionDto.TableName} v ON v.\"BannerId\" = b.\"Id\" " +
            $"INNER JOIN {ZoneAssignmentDto.TableName} za ON za.\"BannerVersionId\" = v.\"Id\" " +
            "WHERE za.\"ZoneId\" = @0 ORDER BY b.\"Key\"",
            zoneId);

    private static ZoneDto ToDto(BannerZone zone) => new()
    {
        Id = zone.Id,
        Key = zone.Key,
        Alias = zone.Alias,
        Name = zone.Name,
        Description = zone.Description,
        ShowCount = zone.ShowCount,
        ImageWidth = zone.ImageWidth,
        ImageHeight = zone.ImageHeight,
        RenderMode = (byte)zone.RenderMode,
        CreateDate = zone.CreateDate,
        UpdateDate = zone.UpdateDate,
    };

    private static BannerZone? Map(ZoneDto? dto) => dto is null
        ? null
        : new BannerZone
        {
            Id = dto.Id,
            Key = dto.Key,
            Alias = dto.Alias,
            Name = dto.Name,
            Description = dto.Description,
            ShowCount = dto.ShowCount,
            ImageWidth = dto.ImageWidth,
            ImageHeight = dto.ImageHeight,
            RenderMode = (ZoneRenderMode)dto.RenderMode,
            CreateDate = Utc(dto.CreateDate),
            UpdateDate = Utc(dto.UpdateDate),
        };
}
