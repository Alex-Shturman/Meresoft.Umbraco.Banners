using System.Text.Json;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Delivery;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Delivery;

[TestFixture]
public class ZoneDeliveryPathBaseTests
{
    [Test]
    public void TheClickAddressOfADeliveredBanner_StartsWithThePathBase()
    {
        var banner = new SelectedBanner { BannerId = 12, ZoneId = 3, ImageUrl = "/m/x.png", AltText = "alt", LinkUrl = "https://example.org/x" };
        var zone = new SnapshotZone(3, "home-top", 1, ZoneRenderMode.Client, null, null, []);
        ILiveSnapshotAccessor snapshot = Substitute.For<ILiveSnapshotAccessor>();
        snapshot.Current.Returns(new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>()));
        IBannerSelector selector = Substitute.For<IBannerSelector>();
        selector.Select("home-top").Returns([banner]);
        IViewTokenProtector tokens = Substitute.For<IViewTokenProtector>();
        tokens.Issue(Arg.Any<int>(), Arg.Any<IReadOnlyList<int>>()).Returns("token");
        var http = new DefaultHttpContext();
        http.Request.PathBase = "/site";
        var controller = new ZoneDeliveryController(selector, snapshot, tokens, Options.Create(new BannersOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };

        var result = (ContentResult)controller.Zone("home-top");

        using JsonDocument json = JsonDocument.Parse(result.Content!);
        string href = json.RootElement.GetProperty("banners")[0].GetProperty("clickUrl").GetString()!;
        Assert.That(href, Is.EqualTo("/site/_banners/c/12?z=3"));
    }
}
