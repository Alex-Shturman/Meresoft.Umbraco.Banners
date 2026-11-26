using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Delivery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Delivery;

[TestFixture]
public class ClickControllerRedirectTests
{
    private static string? RedirectFor(string storedLink)
    {
        IBannerLinkLookup links = Substitute.For<IBannerLinkLookup>();
        links.Find(7, 1).Returns(new ClickTarget(storedLink, Countable: false));
        var controller = new ClickController(links, new BotMatcher(Options.Create(new BannersOptions())), new CounterBuffer(TimeProvider.System), TimeProvider.System)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        return (controller.Click("7", "1") as RedirectResult)?.Url;
    }

    [TestCase("HTTPS://EXAMPLE.ORG/Path?x=1", "https://example.org/Path?x=1")]
    [TestCase("https://example.org/a%20b", "https://example.org/a%20b")]
    [TestCase("https://example.org", "https://example.org/")]
    public void TheRedirectGoesToTheNormalisedAbsoluteUri(string stored, string expected) =>
        Assert.That(RedirectFor(stored), Is.EqualTo(expected));
}
