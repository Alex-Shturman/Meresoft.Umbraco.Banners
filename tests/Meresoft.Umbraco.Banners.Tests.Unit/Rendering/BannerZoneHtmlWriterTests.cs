using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Rendering;

[TestFixture]
public class BannerZoneHtmlWriterTests
{
    private const string ScriptTag = "<script src=\"/meresoft-banners/banners.js\" defer></script>";

    private BannerZoneHtmlWriter _writer = null!;
    private Dictionary<object, object?> _requestItems = null!;

    [SetUp]
    public void SetUp()
    {
        _writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions()));
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
            "<div class=\"mb-zone my-ads\" data-mbz=\"search-top\" data-mb-base=\"/_banners\">"
            + "<div class=\"mb-banner\" data-mbb=\"12\">"
            + "<a href=\"https://example.org/show\" rel=\"sponsored noopener\">"
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
        Assert.That(html, Does.Contain("href=\"https://example.org/a?b=1&amp;c=&quot;2&quot;\""));
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
        var writer = new BannerZoneHtmlWriter(Options.Create(new BannersOptions { PublicPath = "/ads-x" }));

        string html = writer.Render(new BannerZoneView("z", null, [Banner(1)], null), new Dictionary<object, object?>());

        Assert.That(html, Does.Contain("data-mb-base=\"/ads-x\""));
    }
}
