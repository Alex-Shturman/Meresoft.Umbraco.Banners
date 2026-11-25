using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>Shared helpers of the service level integration tests: services, a user and invented data.</summary>
public abstract class BannersServiceTestBase : BannersIntegrationTestBase
{
    protected static readonly Guid User = Constants.Security.SuperUserKey;

    protected IBannerZoneService ZoneService => GetRequiredService<IBannerZoneService>();

    protected IBannerService BannerService => GetRequiredService<IBannerService>();

    [SetUp]
    public async Task InstallSchema() => await InstallPackageAsync();

    /// <summary>Creates an image media item and returns its key.</summary>
    protected Guid NewMediaKey() => TestMedia.CreateImage(GetRequiredService<IMediaService>()).Key;

    /// <summary>Creates a zone through the service and returns it.</summary>
    protected async Task<BannerZone> NewZoneAsync(string alias, int showCount = 1, int? width = null, int? height = null)
    {
        var attempt = await ZoneService.CreateAsync(
            new BannerZoneModel { Alias = alias, Name = "Zone " + alias, ShowCount = showCount, ImageWidth = width, ImageHeight = height },
            User);
        Assert.That(attempt.Success, Is.True, $"zone {alias}: {attempt.Status}");
        return attempt.Result!;
    }

    /// <summary>A valid image draft assigned to the given zones.</summary>
    protected static BannerDraftModel ImageDraft(Guid mediaKey, params Guid[] zoneKeys) => new()
    {
        Name = "Summer promo",
        MediaKey = mediaKey,
        LinkUrl = "https://example.org/show",
        AltText = "Invented summer promo",
        Weight = 1,
        ZoneKeys = zoneKeys,
    };

    /// <summary>Creates an image banner (a draft) through the service and returns it.</summary>
    protected async Task<Banner> NewBannerAsync(BannerDraftModel draft)
    {
        var attempt = await BannerService.CreateAsync(new BannerCreateModel { Type = BannerType.Image, Draft = draft }, User);
        Assert.That(attempt.Success, Is.True, $"banner: {attempt.Status}");
        return attempt.Result!;
    }
}
