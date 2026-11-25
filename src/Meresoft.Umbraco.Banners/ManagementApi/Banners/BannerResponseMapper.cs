using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
using Meresoft.Umbraco.Banners.Services;

namespace Meresoft.Umbraco.Banners.ManagementApi.Banners;

/// <summary>Maps a banner to its response, resolving zone ids to keys.</summary>
public sealed class BannerResponseMapper
{
    private readonly IBannerZoneService _zones;

    public BannerResponseMapper(IBannerZoneService zones) => _zones = zones;

    public async Task<BannerResponseModel> MapAsync(Banner banner)
    {
        IReadOnlyList<BannerZone> zones = await _zones.GetAllAsync();
        return BannerResponseModel.From(banner, zones.ToDictionary(z => z.Id, z => z.Key));
    }
}
