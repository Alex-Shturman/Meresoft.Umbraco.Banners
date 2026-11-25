using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using NPoco;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <inheritdoc cref="IBannerRepository" />
internal sealed class BannerRepository : RepositoryBase, IBannerRepository
{
    private const int MaxPageSize = 100;

    public BannerRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public Banner Add(Banner banner)
    {
        ArgumentNullException.ThrowIfNull(banner);

        BannerDto dto = ToDto(banner);
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
            InsertVersion(version);
        }

        return banner;
    }

    public Banner? GetByKey(Guid key) =>
        LoadOne(Database.FirstOrDefault<BannerDto>("WHERE \"Key\" = @0", key));

    public Banner? GetById(int id) =>
        LoadOne(Database.FirstOrDefault<BannerDto>("WHERE \"Id\" = @0", id));

    public void SaveDraft(Banner banner)
    {
        ArgumentNullException.ThrowIfNull(banner);
        if (banner.Draft is null)
        {
            throw new InvalidOperationException("The banner has no draft version to save.");
        }

        Database.Execute(
            $"UPDATE {BannerDto.TableName} SET \"Name\" = @0, \"UpdateDate\" = @1, \"UpdatedBy\" = @2 WHERE \"Id\" = @3",
            banner.Name,
            banner.UpdateDate,
            banner.UpdatedBy,
            banner.Id);

        banner.Draft.BannerId = banner.Id;
        banner.Draft.Kind = BannerVersionKind.Draft;
        Database.Update(ToDto(banner.Draft));
        ReplaceAssignments(banner.Draft.Id, banner.Draft.ZoneIds);
    }

    public void CopyDraftOverLive(int bannerId, DateTime publishDateUtc)
    {
        BannerVersionDto draft = Database.FirstOrDefault<BannerVersionDto>(
            "WHERE \"BannerId\" = @0 AND \"Kind\" = @1", bannerId, (byte)BannerVersionKind.Draft)
            ?? throw new InvalidOperationException("The banner has no draft version to publish.");
        List<int> zoneIds = LoadZoneIds([draft.Id]).GetValueOrDefault(draft.Id) ?? [];

        BannerVersionDto? live = Database.FirstOrDefault<BannerVersionDto>(
            "WHERE \"BannerId\" = @0 AND \"Kind\" = @1", bannerId, (byte)BannerVersionKind.Live);

        BannerVersionDto copy = CloneContent(draft, BannerVersionKind.Live);
        if (live is null)
        {
            Database.Insert(copy);
        }
        else
        {
            copy.Id = live.Id;
            Database.Update(copy);
        }

        ReplaceAssignments(copy.Id, zoneIds);

        Database.Execute(
            $"UPDATE {BannerDto.TableName} SET \"IsPublished\" = @0, \"PublishDate\" = @1 WHERE \"Id\" = @2",
            true,
            publishDateUtc,
            bannerId);
    }

    public void SetPublished(int bannerId, bool isPublished) =>
        Database.Execute($"UPDATE {BannerDto.TableName} SET \"IsPublished\" = @0 WHERE \"Id\" = @1", isPublished, bannerId);

    public void Delete(int id)
    {
        // Explicit deletes, in dependency order, so the result does not depend on the database
        // enforcing the cascade rules. Statistic rows have no foreign key and are not touched.
        Database.Execute(
            $"DELETE FROM {ZoneAssignmentDto.TableName} WHERE \"BannerVersionId\" IN " +
            $"(SELECT \"Id\" FROM {BannerVersionDto.TableName} WHERE \"BannerId\" = @0)",
            id);
        Database.Execute($"DELETE FROM {BannerVersionDto.TableName} WHERE \"BannerId\" = @0", id);
        Database.Execute($"DELETE FROM {BannerDto.TableName} WHERE \"Id\" = @0", id);
    }

    public (IReadOnlyList<Banner> Items, long Total) GetPaged(BannerFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var where = new List<string>();
        var args = new List<object>();

        if (filter.Type is { } type)
        {
            where.Add($"b.\"Type\" = @{args.Count}");
            args.Add((byte)type);
        }

        if (filter.ZoneId is { } zoneId)
        {
            where.Add(
                $"EXISTS (SELECT 1 FROM {ZoneAssignmentDto.TableName} za " +
                $"WHERE za.\"ZoneId\" = @{args.Count} AND za.\"BannerVersionId\" = COALESCE(l.\"Id\", d.\"Id\"))");
            args.Add(zoneId);
        }

        if (filter.State is { } state)
        {
            where.Add(state switch
            {
                BannerState.Draft => "l.\"Id\" IS NULL",
                BannerState.Unpublished => "l.\"Id\" IS NOT NULL AND b.\"IsPublished\" = 0",
                BannerState.Published =>
                    "l.\"Id\" IS NOT NULL AND b.\"IsPublished\" = 1 AND " +
                    "(d.\"Id\" IS NULL OR b.\"PublishDate\" IS NULL OR d.\"SavedDate\" <= b.\"PublishDate\")",
                BannerState.PublishedWithChanges =>
                    "l.\"Id\" IS NOT NULL AND b.\"IsPublished\" = 1 AND " +
                    "d.\"Id\" IS NOT NULL AND b.\"PublishDate\" IS NOT NULL AND d.\"SavedDate\" > b.\"PublishDate\"",
                _ => throw new ArgumentOutOfRangeException(nameof(filter), "Unknown banner state."),
            });
        }

        string from =
            $"FROM {BannerDto.TableName} b " +
            $"LEFT JOIN {BannerVersionDto.TableName} d ON d.\"BannerId\" = b.\"Id\" AND d.\"Kind\" = {(byte)BannerVersionKind.Draft} " +
            $"LEFT JOIN {BannerVersionDto.TableName} l ON l.\"BannerId\" = b.\"Id\" AND l.\"Kind\" = {(byte)BannerVersionKind.Live}" +
            (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty);

        long total = Database.ExecuteScalar<long>($"SELECT COUNT(*) {from}", args.ToArray());

        int take = Math.Clamp(filter.Take, 1, MaxPageSize);
        var select = new Sql(
            "SELECT b.\"Id\", b.\"Key\", b.\"Name\", b.\"Type\", b.\"IsPublished\", b.\"CreateDate\", b.\"UpdateDate\", " +
            $"b.\"CreatedBy\", b.\"UpdatedBy\", b.\"PublishDate\" {from} ORDER BY b.\"UpdateDate\" DESC, b.\"Id\" DESC",
            args.ToArray());
        List<BannerDto> rows = Database.SkipTake<BannerDto>(Math.Max(filter.Skip, 0), take, select);

        return (LoadMany(rows), total);
    }

    private Banner? LoadOne(BannerDto? dto) => dto is null ? null : LoadMany([dto])[0];

    private List<Banner> LoadMany(List<BannerDto> rows)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        int[] bannerIds = rows.Select(r => r.Id).ToArray();
        List<BannerVersionDto> versions = Database.Fetch<BannerVersionDto>("WHERE \"BannerId\" IN (@ids)", new { ids = bannerIds });
        Dictionary<int, List<int>> zones = LoadZoneIds(versions.Select(v => v.Id).ToArray());

        BannerVersion ToVersion(BannerVersionDto v) => Map(v, zones.GetValueOrDefault(v.Id) ?? []);
        ILookup<int, BannerVersionDto> byBanner = versions.ToLookup(v => v.BannerId);

        return rows.Select(row =>
        {
            Banner banner = Map(row);
            foreach (BannerVersionDto v in byBanner[row.Id])
            {
                if (v.Kind == (byte)BannerVersionKind.Draft)
                {
                    banner.Draft = ToVersion(v);
                }
                else
                {
                    banner.Live = ToVersion(v);
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
            .Fetch<ZoneAssignmentDto>("WHERE \"BannerVersionId\" IN (@ids)", new { ids = versionIds })
            .GroupBy(a => a.BannerVersionId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.ZoneId).OrderBy(z => z).ToList());
    }

    private void InsertVersion(BannerVersion version)
    {
        BannerVersionDto dto = ToDto(version);
        Database.Insert(dto);
        version.Id = dto.Id;
        ReplaceAssignments(dto.Id, version.ZoneIds);
    }

    private void ReplaceAssignments(int versionId, IEnumerable<int> zoneIds)
    {
        Database.Execute($"DELETE FROM {ZoneAssignmentDto.TableName} WHERE \"BannerVersionId\" = @0", versionId);
        foreach (int zoneId in zoneIds.Distinct())
        {
            Database.Insert(new ZoneAssignmentDto { BannerVersionId = versionId, ZoneId = zoneId });
        }
    }

    private static BannerVersionDto CloneContent(BannerVersionDto source, BannerVersionKind kind) => new()
    {
        BannerId = source.BannerId,
        Kind = (byte)kind,
        MediaKey = source.MediaKey,
        Html = source.Html,
        LinkUrl = source.LinkUrl,
        OpenInNewWindow = source.OpenInNewWindow,
        AltText = source.AltText,
        Title = source.Title,
        Weight = source.Weight,
        StartUtc = source.StartUtc,
        EndUtc = source.EndUtc,
        ImpressionLimit = source.ImpressionLimit,
        ClickLimit = source.ClickLimit,
        SavedDate = source.SavedDate,
        SavedBy = source.SavedBy,
    };

    private static BannerDto ToDto(Banner banner) => new()
    {
        Id = banner.Id,
        Key = banner.Key,
        Name = banner.Name,
        Type = (byte)banner.Type,
        IsPublished = banner.IsPublished,
        CreateDate = banner.CreateDate,
        UpdateDate = banner.UpdateDate,
        CreatedBy = banner.CreatedBy,
        UpdatedBy = banner.UpdatedBy,
        PublishDate = banner.PublishDate,
    };

    private static BannerVersionDto ToDto(BannerVersion version) => new()
    {
        Id = version.Id,
        BannerId = version.BannerId,
        Kind = (byte)version.Kind,
        MediaKey = version.MediaKey,
        Html = version.Html,
        LinkUrl = version.LinkUrl,
        OpenInNewWindow = version.OpenInNewWindow,
        AltText = version.AltText,
        Title = version.Title,
        Weight = version.Weight,
        StartUtc = version.StartUtc,
        EndUtc = version.EndUtc,
        ImpressionLimit = version.ImpressionLimit,
        ClickLimit = version.ClickLimit,
        SavedDate = version.SavedDate,
        SavedBy = version.SavedBy,
    };

    private static Banner Map(BannerDto dto) => new()
    {
        Id = dto.Id,
        Key = dto.Key,
        Name = dto.Name,
        Type = (BannerType)dto.Type,
        IsPublished = dto.IsPublished,
        CreateDate = Utc(dto.CreateDate),
        UpdateDate = Utc(dto.UpdateDate),
        CreatedBy = dto.CreatedBy,
        UpdatedBy = dto.UpdatedBy,
        PublishDate = Utc(dto.PublishDate),
    };

    private static BannerVersion Map(BannerVersionDto dto, List<int> zoneIds) => new()
    {
        Id = dto.Id,
        BannerId = dto.BannerId,
        Kind = (BannerVersionKind)dto.Kind,
        MediaKey = dto.MediaKey,
        Html = dto.Html,
        LinkUrl = dto.LinkUrl,
        OpenInNewWindow = dto.OpenInNewWindow,
        AltText = dto.AltText,
        Title = dto.Title,
        Weight = dto.Weight,
        StartUtc = Utc(dto.StartUtc),
        EndUtc = Utc(dto.EndUtc),
        ImpressionLimit = dto.ImpressionLimit,
        ClickLimit = dto.ClickLimit,
        SavedDate = Utc(dto.SavedDate),
        SavedBy = dto.SavedBy,
        ZoneIds = zoneIds,
    };
}
