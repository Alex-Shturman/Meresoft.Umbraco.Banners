using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <inheritdoc cref="IBannerRepository" />
internal sealed class BannerRepository : RepositoryBase, IBannerRepository
{
    public BannerRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public Banner Add(Banner banner)
    {
        ArgumentNullException.ThrowIfNull(banner);

        BannerDto dto = BannerMapper.ToDto(banner);
        Database.Insert(dto);
        banner.Id = dto.Id;

        foreach ((BannerVersion? version, BannerVersionKind kind) in new[] { (banner.Draft, BannerVersionKind.Draft), (banner.Live, BannerVersionKind.Live) })
        {
            if (version is null)
            {
                continue;
            }

            version.BannerId = banner.Id;
            version.Kind = kind;
            BannerVersionDto versionDto = BannerMapper.ToDto(version);
            Database.Insert(versionDto);
            version.Id = versionDto.Id;
            ReplaceAssignments(versionDto.Id, version.ZoneIds);
        }

        return banner;
    }

    public Banner? GetByKey(Guid key) =>
        LoadOne(Database.FirstOrDefault<BannerDto>(SelectBanners().Where<BannerDto>(b => b.Key == key)));

    public Banner? GetById(int id) =>
        LoadOne(Database.FirstOrDefault<BannerDto>(SelectBanners().Where<BannerDto>(b => b.Id == id)));

    public void SaveDraft(Banner banner)
    {
        ArgumentNullException.ThrowIfNull(banner);
        RequireRow(SaveDraftCore(banner, null) ? 1 : 0, "Banner", banner.Id);
    }

    public bool SaveDraft(Banner banner, DateTime expectedUpdateDate)
    {
        ArgumentNullException.ThrowIfNull(banner);
        return SaveDraftCore(banner, UtcTimestamp.Truncate(expectedUpdateDate));
    }

    public void CopyDraftOverLive(int bannerId, DateTime publishDateUtc) =>
        RequireRow(CopyDraftOverLiveCore(bannerId, publishDateUtc, null) ? 1 : 0, "Banner", bannerId);

    public bool CopyDraftOverLive(int bannerId, DateTime publishDateUtc, DateTime expectedUpdateDate) =>
        CopyDraftOverLiveCore(bannerId, publishDateUtc, UtcTimestamp.Truncate(expectedUpdateDate));

    private bool SaveDraftCore(Banner banner, DateTime? expectedUpdateDate)
    {
        if (banner.Draft is null)
        {
            throw new InvalidOperationException("The banner has no draft version to save.");
        }

        // The banner row update is the first write and the guard: it only matches while nobody changed the banner
        // since it was loaded. Only the columns this save owns are written, so a concurrent change to another column is not overwritten.
        DateTime updateDate = UtcTimestamp.Truncate(banner.UpdateDate);
        Guid updatedBy = banner.UpdatedBy;
        string name = banner.Name;
        int id = banner.Id;
        Sql<ISqlContext> update = NewSql()
            .Update<BannerDto>(u => u.Set(b => b.Name, name).Set(b => b.UpdateDate, updateDate).Set(b => b.UpdatedBy, updatedBy))
            .Where<BannerDto>(b => b.Id == id);
        if (expectedUpdateDate is { } expected)
        {
            update = update.Where<BannerDto>(b => b.UpdateDate == expected);
        }

        if (Database.Execute(update) == 0)
        {
            return false;
        }

        banner.Draft.BannerId = banner.Id;
        banner.Draft.Kind = BannerVersionKind.Draft;
        RequireRow(Database.Update(BannerMapper.ToDto(banner.Draft)), "Draft version", banner.Draft.Id);
        ReplaceAssignments(banner.Draft.Id, banner.Draft.ZoneIds);
        return true;
    }

    private bool CopyDraftOverLiveCore(int bannerId, DateTime publishDateUtc, DateTime? expectedUpdateDate)
    {
        // The banner row update is the first write and the guard, as in SaveDraft.
        DateTime publishDate = UtcTimestamp.Truncate(publishDateUtc);
        Sql<ISqlContext> mark = NewSql()
            .Update<BannerDto>(u => u.Set(b => b.IsPublished, true).Set(b => b.PublishDate, (DateTime?)publishDate))
            .Where<BannerDto>(b => b.Id == bannerId);
        if (expectedUpdateDate is { } expected)
        {
            mark = mark.Where<BannerDto>(b => b.UpdateDate == expected);
        }

        if (Database.Execute(mark) == 0)
        {
            return false;
        }

        BannerVersionDto draft = FindVersion(bannerId, BannerVersionKind.Draft)
            ?? throw new InvalidOperationException($"Banner {bannerId} has no draft version to publish.");
        List<int> zoneIds = LoadZoneIds([draft.Id]).GetValueOrDefault(draft.Id) ?? [];

        BannerVersionDto copy = BannerMapper.CloneContent(draft, BannerVersionKind.Live);
        BannerVersionDto? live = FindVersion(bannerId, BannerVersionKind.Live);
        if (live is null)
        {
            Database.Insert(copy);
        }
        else
        {
            copy.Id = live.Id;
            RequireRow(Database.Update(copy), "Live version", live.Id);
        }

        ReplaceAssignments(copy.Id, zoneIds);
        return true;
    }

    public void SetPublished(int bannerId, bool isPublished)
    {
        RequireRow(
            Database.Execute(NewSql()
                .Update<BannerDto>(u => u.Set(b => b.IsPublished, isPublished))
                .Where<BannerDto>(b => b.Id == bannerId)),
            "Banner",
            bannerId);
    }

    public void Delete(int id)
    {
        // Explicit deletes, in dependency order, so the result does not depend on the database
        // enforcing the cascade rules. Statistic rows have no foreign key and are not touched.
        Sql<ISqlContext> versionIds = NewSql()
            .Select<BannerVersionDto>(v => v.Id)
            .From<BannerVersionDto>()
            .Where<BannerVersionDto>(v => v.BannerId == id);
        Database.Execute(NewSql().Delete<ZoneAssignmentDto>().WhereIn<ZoneAssignmentDto>(a => a.BannerVersionId, versionIds));
        Database.Execute(NewSql().Delete<BannerVersionDto>().Where<BannerVersionDto>(v => v.BannerId == id));
        Database.Execute(NewSql().Delete<BannerDto>().Where<BannerDto>(b => b.Id == id));
    }

    public IReadOnlyList<Banner> GetAllPublished() =>
        LoadMany(Database.Fetch<BannerDto>(SelectBanners().Where<BannerDto>(b => b.IsPublished)))
            .Where(b => b.Live is not null)
            .ToList();

    public IReadOnlyDictionary<int, (Guid Key, string Name)> GetNames(IReadOnlyCollection<int> ids)
    {
        var names = new Dictionary<int, (Guid Key, string Name)>();
        // SQL Server allows about 2,100 parameters in a statement; a list of ids is cut into parts well below that.
        foreach (int[] part in ids.Distinct().Chunk(1_000))
        {
            foreach (BannerDto dto in Database.Fetch<BannerDto>(NewSql().Select<BannerDto>(b => b.Id, b => b.Key, b => b.Name).From<BannerDto>().WhereIn<BannerDto>(b => b.Id, part)))
            {
                names[dto.Id] = (dto.Key, dto.Name);
            }
        }

        return names;
    }

    public int GetMaxId() =>
        Database.ExecuteScalar<int?>($"SELECT MAX({Syntax.GetQuotedColumnName("Id")}) FROM {Syntax.GetQuotedTableName(BannerDto.TableName)}") ?? 0;

    public (IReadOnlyList<Banner> Items, long Total) GetPaged(BannerFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var queries = new BannerQueryBuilder(Syntax);
        (int skip, int take) = BannerQueryBuilder.Page(filter);

        long total = Database.ExecuteScalar<long>(queries.Count(filter));
        List<BannerDto> rows = Database.SkipTake<BannerDto>(skip, take, queries.Select(filter));

        return (LoadMany(rows), total);
    }

    private Sql<ISqlContext> SelectBanners() => NewSql().Select<BannerDto>().From<BannerDto>();

    private BannerVersionDto? FindVersion(int bannerId, BannerVersionKind kind)
    {
        byte kindValue = (byte)kind;
        return Database.FirstOrDefault<BannerVersionDto>(
            NewSql().Select<BannerVersionDto>().From<BannerVersionDto>()
                .Where<BannerVersionDto>(v => v.BannerId == bannerId && v.Kind == kindValue));
    }

    private Banner? LoadOne(BannerDto? dto) => dto is null ? null : LoadMany([dto])[0];

    private List<Banner> LoadMany(List<BannerDto> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        int[] bannerIds = rows.Select(r => r.Id).ToArray();
        List<BannerVersionDto> versions = Database.Fetch<BannerVersionDto>(
            NewSql().Select<BannerVersionDto>().From<BannerVersionDto>().WhereIn<BannerVersionDto>(v => v.BannerId, bannerIds));
        Dictionary<int, List<int>> zones = LoadZoneIds(versions.Select(v => v.Id).ToArray());

        ILookup<int, BannerVersionDto> byBanner = versions.ToLookup(v => v.BannerId);

        return rows.Select(row =>
        {
            Banner banner = BannerMapper.Map(row);
            foreach (BannerVersionDto v in byBanner[row.Id])
            {
                BannerVersion version = BannerMapper.Map(v, zones.GetValueOrDefault(v.Id) ?? []);
                if (v.Kind == (byte)BannerVersionKind.Draft)
                {
                    banner.Draft = version;
                }
                else
                {
                    banner.Live = version;
                }
            }

            return banner;
        }).ToList();
    }

    private Dictionary<int, List<int>> LoadZoneIds(int[] versionIds)
    {
        if (versionIds.Length == 0)
        {
            return [];
        }

        return Database
            .Fetch<ZoneAssignmentDto>(
                NewSql().Select<ZoneAssignmentDto>().From<ZoneAssignmentDto>().WhereIn<ZoneAssignmentDto>(a => a.BannerVersionId, versionIds))
            .GroupBy(a => a.BannerVersionId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.ZoneId).OrderBy(z => z).ToList());
    }

    private void ReplaceAssignments(int versionId, IEnumerable<int> zoneIds)
    {
        Database.Execute(NewSql().Delete<ZoneAssignmentDto>().Where<ZoneAssignmentDto>(a => a.BannerVersionId == versionId));
        foreach (int zoneId in zoneIds.Distinct())
        {
            Database.Insert(new ZoneAssignmentDto { BannerVersionId = versionId, ZoneId = zoneId });
        }
    }
}
