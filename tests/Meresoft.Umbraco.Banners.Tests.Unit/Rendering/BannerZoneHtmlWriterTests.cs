using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Rendering;

[TestFixture]
public class BannerZoneHtmlWriterTests
{
    private const string ScriptTag = "<script src=\"/meresoft-banners/banners.js\" defer></script>";

    private BannerZoneHtmlWriter _writer = null!;
    private FakeProtector _tokens = null!;
    private Dictionary<object, object?> _requestItems = null!;

    [SetUp]
    public void SetUp()
    {
        _tokens = new FakeProtector();
        _writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions()), _tokens);
        _requestItems = [];
    }

    private static SelectedBanner Banner(int id, Func<SelectedBanner, SelectedBanner>? change = null)
    {
        var banner = new SelectedBanner
        {
            BannerId = id,
            ZoneId = 3,
            ImageUrl = "/media/abc/banner.png?width=728&height=90",
            Width = 728,
            Height = 90,
            AltText = "Invented summer promo 2027",
            LinkUrl = "https://example.org/show",
        };
        return change is null ? banner : change(banner);
    }

    private string Render(string alias, string? cssClass, IReadOnlyList<SelectedBanner> banners, string? fallback = null) =>
        _writer.Render(new BannerZoneView(alias, cssClass, banners, fallback), _requestItems);

    [Test]
    public void OneImageBanner_WritesTheContractMarkup()
    {
        string html = Render("search-top", "my-ads", [Banner(12)]);

        Assert.That(html, Is.EqualTo(
            "<div class=\"mb-zone my-ads\" data-mbz=\"search-top\" data-mb-base=\"/_banners\" data-mbz-token=\"token-3-12\">"
            + "<div class=\"mb-banner\" data-mbb=\"12\">"
            + "<a href=\"/_banners/c/12?z=3\" rel=\"sponsored noopener\">"
            + "<img src=\"/media/abc/banner.png?width=728&amp;height=90\" alt=\"Invented summer promo 2027\" width=\"728\" height=\"90\" loading=\"lazy\" decoding=\"async\">"
            + "</a></div></div>"
            + ScriptTag));
    }

    [Test]
    public void WithoutAClass_OnlyTheZoneClassIsWritten()
    {
        string html = Render("search-top", null, [Banner(12)]);

        Assert.That(html, Does.StartWith("<div class=\"mb-zone\" data-mbz=\"search-top\""));
    }

    [Test]
    public void TargetBlank_OnlyWhenTheBannerOpensInANewWindow()
    {
        string same = Render("z", null, [Banner(1)]);
        string newWindow = Render("z", null, [Banner(1, b => b with { OpenInNewWindow = true })]);

        Assert.That(same, Does.Not.Contain("target="));
        Assert.That(newWindow, Does.Contain("rel=\"sponsored noopener\" target=\"_blank\""));
    }

    [Test]
    public void TitleAttribute_OnlyWhenTheBannerHasATitle()
    {
        string without = Render("z", null, [Banner(1)]);
        string with = Render("z", null, [Banner(1, b => b with { Title = "Summer promo" })]);

        Assert.That(without, Does.Not.Contain("title="));
        Assert.That(with, Does.Contain("title=\"Summer promo\""));
    }

    [Test]
    public void ImageWithoutAFixedSize_OmitsWidthAndHeight()
    {
        string html = Render("z", null, [Banner(1, b => b with { Width = null, Height = null })]);

        Assert.That(html, Does.Not.Contain(" width=\""));
        Assert.That(html, Does.Not.Contain(" height=\""));
    }

    [Test]
    public void EveryValueIsHtmlEncoded()
    {
        string html = Render(
            "z\"><script>",
            "x\"y",
            [Banner(1, b => b with
            {
                AltText = "<b>\"alt\"</b>",
                Title = "t\" onmouseover=\"x",
                LinkUrl = "https://example.org/a?b=1&c=\"2\"",
                ImageUrl = "/m.png?a=1&b=\"2\"",
            })]);

        Assert.That(html, Does.Not.Contain("<b>"));
        Assert.That(html, Does.Not.Contain("\"><script>"));
        Assert.That(html, Does.Contain("alt=\"&lt;b&gt;&quot;alt&quot;&lt;/b&gt;\""));
        Assert.That(html, Does.Contain("title=\"t&quot; onmouseover=&quot;x\""));
        Assert.That(html, Does.Contain("src=\"/m.png?a=1&amp;b=&quot;2&quot;\""));
    }

    [Test]
    public void NoBannerAndNoFallback_WritesNothingAtAll()
    {
        string html = Render("z", "c", []);

        Assert.That(html, Is.Empty);
        Assert.That(_requestItems, Is.Empty, "no script flag is set when nothing is written");
    }

    [Test]
    public void NoBanner_WritesTheFallbackInstead_WithoutTheScript()
    {
        string html = Render("z", null, [], "<p>Buy here</p>");

        Assert.That(html, Is.EqualTo("<p>Buy here</p>"));
    }

    [Test]
    public void WithBanners_TheFallbackIsNotWritten()
    {
        string html = Render("z", null, [Banner(1)], "<p>Buy here</p>");

        Assert.That(html, Does.Not.Contain("Buy here"));
    }

    [Test]
    public void TheScriptTag_IsWrittenOncePerRequestEvenWithSeveralZones()
    {
        string first = Render("a", null, [Banner(1)]);
        string second = Render("b", null, [Banner(2)]);

        Assert.That(first, Does.Contain(ScriptTag));
        Assert.That(second, Does.Not.Contain("<script"));
    }

    [Test]
    public void SeveralBanners_AreWrittenInOrderInsideOneZoneElement()
    {
        string html = Render("z", null, [Banner(1), Banner(2)]);

        Assert.That(html.Split("<div class=\"mb-zone").Length, Is.EqualTo(2));
        Assert.That(html.IndexOf("data-mbb=\"1\"", StringComparison.Ordinal), Is.LessThan(html.IndexOf("data-mbb=\"2\"", StringComparison.Ordinal)));
    }

    [Test]
    public void TheBasePathComesFromTheOptions()
    {
        var writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions { PublicPath = "/ads-x" }), new FakeProtector());

        string html = writer.Render(new BannerZoneView("z", null, [Banner(1)], null), new Dictionary<object, object?>());

        Assert.That(html, Does.Contain("data-mb-base=\"/ads-x\""));
    }

    [Test]
    public void TheLinkPointsAtTheClickAddress_NeverAtTheStoredUrl()
    {
        string html = Render("z", null, [Banner(12, b => b with { LinkUrl = "https://example.org/a?b=1&c=2" })]);

        Assert.That(html, Does.Contain("href=\"/_banners/c/12?z=3\""));
        Assert.That(html, Does.Not.Contain("example.org"));
    }

    [Test]
    public void TheClickAddress_UsesTheConfiguredBasePath()
    {
        var writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions { PublicPath = "/ads-x" }), new FakeProtector());

        string html = writer.Render(new BannerZoneView("z", null, [Banner(12)], null), new Dictionary<object, object?>());

        Assert.That(html, Does.Contain("href=\"/ads-x/c/12?z=3\""));
    }

    [Test]
    public void TheToken_CarriesTheZoneAndAllBannerIdsOfTheView()
    {
        string html = Render("z", null, [Banner(12), Banner(15)]);

        Assert.That(_tokens.Issued, Is.EqualTo(new[] { (3, new[] { 12, 15 }) }));
        Assert.That(html, Does.Contain("data-mbz-token=\"token-3-12-15\""));
    }

    [Test]
    public void AZoneWithoutBanners_IssuesNoToken()
    {
        Render("z", null, [], "<p>Fallback</p>");

        Assert.That(_tokens.Issued, Is.Empty);
    }

    private sealed class FakeProtector : IViewTokenProtector
    {
        public List<(int ZoneId, int[] BannerIds)> Issued { get; } = [];

        public string Issue(int zoneId, IReadOnlyList<int> bannerIds)
        {
            Issued.Add((zoneId, bannerIds.ToArray()));
            return "token-" + zoneId + "-" + string.Join('-', bannerIds);
        }

        public bool TryValidate(string? token, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ViewTokenPayload? payload)
        {
            payload = null;
            return false;
        }
    }

    private static SelectedBanner HtmlBanner(int id, string html) => new()
    {
        BannerId = id,
        ZoneId = 3,
        ImageUrl = string.Empty,
        AltText = string.Empty,
        LinkUrl = string.Empty,
        Html = html,
    };

    [Test]
    public void AnHtmlBanner_IsWrittenAsStoredInsideTheContractBox_WithNoClickWrapper()
    {
        // Ad network code is written as it is stored: not encoded, not wrapped in a link (FR-006).
        const string stored = "<div class=\"ad\"><script>var x = 1 < 2;</script><b>Invented offer & more</b></div>";

        string html = Render("side", null, [HtmlBanner(15, stored)]);

        Assert.That(html, Is.EqualTo(
            "<div class=\"mb-zone\" data-mbz=\"side\" data-mb-base=\"/_banners\" data-mbz-token=\"token-3-15\">"
            + "<div class=\"mb-banner mb-html\" data-mbb=\"15\">" + stored + "</div></div>"
            + ScriptTag));
        Assert.That(html, Does.Not.Contain("/c/15"), "no click redirect for html banners");
        Assert.That(html, Does.Not.Contain("<a href"));
    }

    [Test]
    public void AnImageAndAnHtmlBanner_CanShareAZone_InOrder()
    {
        string html = Render("z", null, [Banner(1), HtmlBanner(2, "<i>x</i>")]);

        Assert.That(html, Does.Contain("<div class=\"mb-banner\" data-mbb=\"1\"><a href"));
        Assert.That(html, Does.Contain("<div class=\"mb-banner mb-html\" data-mbb=\"2\"><i>x</i></div>"));
    }

    [Test]
    public void AnHtmlBanner_ThatClosesTheBoxItself_IsEmittedVerbatim()
    {
        // Pins the contract: stored html is trusted and never encoded or repaired (FR-006).
        const string stored = "</div><script>x()</script><div>";

        string html = Render("z", null, [HtmlBanner(1, stored)]);

        Assert.That(html, Does.Contain("data-mbb=\"1\">" + stored + "</div>"));
    }

    private string RenderClient(string alias, string? cssClass, string? fallback, bool deliveryEnabled = true)
    {
        var writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions { DeliveryEndpointEnabled = deliveryEnabled }), _tokens);
        return writer.Render(new BannerZoneView(alias, cssClass, [], fallback, ZoneRenderMode.Client), _requestItems);
    }

    [Test]
    public void ClientMode_WritesThePlaceholder_WithTheFallbackInside_AndNoToken()
    {
        string html = RenderClient("search-top", null, "<p>Fallback</p>");

        Assert.That(html, Is.EqualTo(
            "<div class=\"mb-zone\" data-mbz=\"search-top\" data-mb-base=\"/_banners\" data-mbz-mode=\"client\"><p>Fallback</p></div>" + ScriptTag));
        Assert.That(html, Does.Not.Contain("data-mbz-token"));
        Assert.That(_tokens.Issued, Is.Empty, "the server does not choose banners, so it issues no token");
    }

    [Test]
    public void ClientMode_WithoutFallback_StillWritesTheEmptyPlaceholder_AndTheExtraClass()
    {
        string html = RenderClient("sidebar", "my-ads", null);

        Assert.That(html, Does.StartWith("<div class=\"mb-zone my-ads\" data-mbz=\"sidebar\" data-mb-base=\"/_banners\" data-mbz-mode=\"client\"></div>"));
    }

    [Test]
    public void ClientMode_WritesTheScriptOncePerPage()
    {
        string first = RenderClient("a", null, null);
        string second = RenderClient("b", null, null);

        Assert.That(first, Does.EndWith(ScriptTag));
        Assert.That(second, Does.Not.Contain("<script"));
    }

    [Test]
    public void ClientMode_WithTheDeliveryEndpointDisabled_WritesOnlyTheFallback()
    {
        Assert.That(RenderClient("search-top", "my-ads", "<p>Fallback</p>", deliveryEnabled: false), Is.EqualTo("<p>Fallback</p>"));
        Assert.That(RenderClient("search-top", null, null, deliveryEnabled: false), Is.Empty);
        Assert.That(_requestItems, Is.Empty, "no script tag when there is nothing for it to do");
    }
}
