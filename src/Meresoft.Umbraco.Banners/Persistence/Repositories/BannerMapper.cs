using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>
/// Converts between domain objects and DTOs. Timestamps are truncated to the resolution every
/// database stores exactly (<see cref="UtcTimestamp"/>) and read back as UTC.
/// </summary>
internal static class BannerMapper
{
    public static ZoneDto ToDto(BannerZone zone) => new()
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
        CreateDate = UtcTimestamp.Truncate(zone.CreateDate),
        UpdateDate = UtcTimestamp.Truncate(zone.UpdateDate),
    };

    public static BannerZone? Map(ZoneDto? dto) => dto is null
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

    public static BannerDto ToDto(Banner banner) => new()
    {
        Id = banner.Id,
        Key = banner.Key,
        Name = banner.Name,
        Type = (byte)banner.Type,
        IsPublished = banner.IsPublished,
        CreateDate = UtcTimestamp.Truncate(banner.CreateDate),
        UpdateDate = UtcTimestamp.Truncate(banner.UpdateDate),
        CreatedBy = banner.CreatedBy,
        UpdatedBy = banner.UpdatedBy,
        PublishDate = UtcTimestamp.Truncate(banner.PublishDate),
    };

    public static Banner Map(BannerDto dto) => new()
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

    public static BannerVersionDto ToDto(BannerVersion version) => new()
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
        StartUtc = UtcTimestamp.Truncate(version.StartUtc),
        EndUtc = UtcTimestamp.Truncate(version.EndUtc),
        ImpressionLimit = version.ImpressionLimit,
        ClickLimit = version.ClickLimit,
        SavedDate = UtcTimestamp.Truncate(version.SavedDate),
        SavedBy = version.SavedBy,
    };

    public static BannerVersion Map(BannerVersionDto dto, List<int> zoneIds) => new()
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

    /// <summary>Copies the content of a version row for another kind (draft to live); the id stays unset.</summary>
    public static BannerVersionDto CloneContent(BannerVersionDto source, BannerVersionKind kind) => new()
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

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}
