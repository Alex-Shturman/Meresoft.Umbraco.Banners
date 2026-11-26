using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Zones;
using Meresoft.Umbraco.Banners.ManagementApi.Zones.Models;
using Meresoft.Umbraco.Banners.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.ManagementApi;

[TestFixture]
public class ListZonesControllerTests
{
    private static readonly DateTimeOffset Now = new(2027, 6, 5, 12, 0, 0, TimeSpan.Zero);

    private static SnapshotBanner Banner(int id, DateTime? start = null, DateTime? end = null) => new()
    {
        BannerId = id, ZoneId = 1, ImageUrl = "/m/x.png", AltText = "alt", LinkUrl = "https://example.org/x", StartUtc = start, EndUtc = end,
    };

    private static async Task<ZoneResponseModel[]> ListAsync(params SnapshotBanner[] candidates)
    {
        var zone = new SnapshotZone(1, "home-top", 1, ZoneRenderMode.Server, null, null, candidates);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());
        ILiveSnapshotAccessor accessor = Substitute.For<ILiveSnapshotAccessor>();
        accessor.Current.Returns(snapshot);
        IBannerZoneService zones = Substitute.For<IBannerZoneService>();
        zones.GetAllAsync().Returns(Task.FromResult<IReadOnlyList<BannerZone>>([new BannerZone { Id = 1, Key = Guid.NewGuid(), Alias = "home-top", Name = "Home" }]));

        var controller = new ListZonesController(zones, accessor, new FakeTimeProvider(Now));
        var result = (OkObjectResult)await controller.List();
        return ((IEnumerable<ZoneResponseModel>)result.Value!).ToArray();
    }

    [Test]
    public async Task TheActiveCount_LeavesOutExpiredAndNotYetStartedBanners()
    {
        ZoneResponseModel[] list = await ListAsync(
            Banner(1, end: Now.UtcDateTime.AddMinutes(-1)),
            Banner(2, start: Now.UtcDateTime.AddMinutes(1)),
            Banner(3));

        Assert.That(list.Single().ActiveBannerCount, Is.EqualTo(1));
    }
}
