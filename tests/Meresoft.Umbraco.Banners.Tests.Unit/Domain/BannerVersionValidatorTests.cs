using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Domain;

[TestFixture]
public class BannerVersionValidatorTests
{
    private static BannerVersion ValidImage() => new()
    {
        MediaKey = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        LinkUrl = "https://example.org/show",
        AltText = "Invented boat show 2027",
        Weight = 1,
    };

    private static BannerVersion ValidHtml() => new()
    {
        Html = "<p>Invented offer</p>",
        Weight = 1,
    };

    private static FieldValidationResult ValidateImage(Action<BannerVersion>? change = null)
    {
        BannerVersion version = ValidImage();
        change?.Invoke(version);
        return BannerVersionValidator.Validate(BannerType.Image, version);
    }

    private static FieldValidationResult ValidateHtml(Action<BannerVersion>? change = null)
    {
        BannerVersion version = ValidHtml();
        change?.Invoke(version);
        return BannerVersionValidator.Validate(BannerType.Html, version);
    }

    [Test]
    public void ValidImageVersion_Passes() => Assert.That(ValidateImage().IsValid, Is.True);

    [Test]
    public void ValidHtmlVersion_Passes() => Assert.That(ValidateHtml().IsValid, Is.True);

    [Test]
    public void Defaults_WeightIsOne_DraftKind()
    {
        var version = new BannerVersion();

        Assert.Multiple(() =>
        {
            Assert.That(version.Weight, Is.EqualTo(1));
            Assert.That(version.Kind, Is.EqualTo(BannerVersionKind.Draft));
            Assert.That(version.OpenInNewWindow, Is.False);
        });
    }

    [Test]
    public void Image_MediaKeyRequired()
    {
        Assert.That(ValidateImage(v => v.MediaKey = null).Errors, Contains.Key("mediaKey"));
        Assert.That(ValidateImage(v => v.MediaKey = Guid.Empty).Errors, Contains.Key("mediaKey"));
    }

    [Test]
    public void Html_MediaKeyNotRequired() => Assert.That(ValidateHtml(v => v.MediaKey = null).IsValid, Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Html_HtmlRequired(string? html)
    {
        Assert.That(ValidateHtml(v => v.Html = html).Errors, Contains.Key("html"));
    }

    [Test]
    public void Image_HtmlNotRequired() => Assert.That(ValidateImage(v => v.Html = null).IsValid, Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Image_LinkUrlRequired(string? url)
    {
        Assert.That(ValidateImage(v => v.LinkUrl = url).Errors, Contains.Key("linkUrl"));
    }

    [TestCase("https://example.org/show")]
    [TestCase("http://example.org")]
    [TestCase("HTTPS://EXAMPLE.ORG/a?b=c&d=e#f")]
    public void LinkUrl_AbsoluteHttpOrHttps_Valid(string url)
    {
        Assert.That(ValidateImage(v => v.LinkUrl = url).IsValid, Is.True);
    }

    [TestCase("/relative/path")]
    [TestCase("example.org/show")]
    [TestCase("javascript:alert(1)")]
    [TestCase("ftp://example.org/file")]
    [TestCase("mailto:someone@example.org")]
    [TestCase("data:text/html,<p>x</p>")]
    [TestCase("//example.org/show")]
    [TestCase("https://")]
    [TestCase("not a url")]
    public void LinkUrl_NotAbsoluteHttp_Refused(string url)
    {
        Assert.That(ValidateImage(v => v.LinkUrl = url).Errors, Contains.Key("linkUrl"));
    }

    [Test]
    public void LinkUrl_Max2048()
    {
        string prefix = "https://example.org/";
        Assert.Multiple(() =>
        {
            Assert.That(ValidateImage(v => v.LinkUrl = prefix + new string('a', 2048 - prefix.Length)).IsValid, Is.True);
            Assert.That(ValidateImage(v => v.LinkUrl = prefix + new string('a', 2049 - prefix.Length)).Errors, Contains.Key("linkUrl"));
        });
    }

    [Test]
    public void Html_LinkUrlOptional_ButMustBeValidWhenGiven()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ValidateHtml(v => v.LinkUrl = null).IsValid, Is.True);
            Assert.That(ValidateHtml(v => v.LinkUrl = "javascript:alert(1)").Errors, Contains.Key("linkUrl"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Image_AltTextRequired(string? alt)
    {
        Assert.That(ValidateImage(v => v.AltText = alt!).Errors, Contains.Key("altText"));
    }

    [Test]
    public void Html_AltTextNotRequired() => Assert.That(ValidateHtml(v => v.AltText = string.Empty).IsValid, Is.True);

    [Test]
    public void AltText_Max255()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ValidateImage(v => v.AltText = new string('a', 255)).IsValid, Is.True);
            Assert.That(ValidateImage(v => v.AltText = new string('a', 256)).Errors, Contains.Key("altText"));
        });
    }

    [Test]
    public void Title_Nullable_Max255()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ValidateImage(v => v.Title = null).IsValid, Is.True);
            Assert.That(ValidateImage(v => v.Title = new string('t', 255)).IsValid, Is.True);
            Assert.That(ValidateImage(v => v.Title = new string('t', 256)).Errors, Contains.Key("title"));
        });
    }

    [TestCase(1)]
    [TestCase(50)]
    [TestCase(100)]
    public void Weight_Valid(int weight) => Assert.That(ValidateImage(v => v.Weight = weight).IsValid, Is.True);

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public void Weight_Invalid(int weight) => Assert.That(ValidateImage(v => v.Weight = weight).Errors, Contains.Key("weight"));

    [Test]
    public void Schedule_EndMustBeAfterStart_WhenBothSet()
    {
        var start = new DateTime(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        Assert.Multiple(() =>
        {
            Assert.That(ValidateImage(v => { v.StartUtc = start; v.EndUtc = start.AddHours(1); }).IsValid, Is.True);
            Assert.That(ValidateImage(v => { v.StartUtc = start; v.EndUtc = start; }).Errors, Contains.Key("endUtc"));
            Assert.That(ValidateImage(v => { v.StartUtc = start; v.EndUtc = start.AddSeconds(-1); }).Errors, Contains.Key("endUtc"));
        });
    }

    [Test]
    public void Schedule_OnlyOneSet_IsValid()
    {
        var moment = new DateTime(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        Assert.Multiple(() =>
        {
            Assert.That(ValidateImage(v => v.StartUtc = moment).IsValid, Is.True);
            Assert.That(ValidateImage(v => v.EndUtc = moment).IsValid, Is.True);
        });
    }

    [TestCase(null)]
    [TestCase(1L)]
    [TestCase(5_000_000_000L)]
    public void ImpressionLimit_Valid(long? limit) => Assert.That(ValidateImage(v => v.ImpressionLimit = limit).IsValid, Is.True);

    [TestCase(0L)]
    [TestCase(-1L)]
    public void ImpressionLimit_Invalid(long limit) => Assert.That(ValidateImage(v => v.ImpressionLimit = limit).Errors, Contains.Key("impressionLimit"));

    [TestCase(null)]
    [TestCase(1L)]
    public void ClickLimit_Valid_ForImage(long? limit) => Assert.That(ValidateImage(v => v.ClickLimit = limit).IsValid, Is.True);

    [TestCase(0L)]
    [TestCase(-1L)]
    public void ClickLimit_Invalid(long limit) => Assert.That(ValidateImage(v => v.ClickLimit = limit).Errors, Contains.Key("clickLimit"));

    [Test]
    public void ClickLimit_MustBeNull_ForHtml()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ValidateHtml(v => v.ClickLimit = null).IsValid, Is.True);
            Assert.That(ValidateHtml(v => v.ClickLimit = 10).Errors, Contains.Key("clickLimit"));
        });
    }

    [Test]
    public void ImpressionLimit_AllowedForHtml() => Assert.That(ValidateHtml(v => v.ImpressionLimit = 10).IsValid, Is.True);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void BannerName_Required(string? name)
    {
        Assert.That(BannerVersionValidator.ValidateName(name).Errors, Contains.Key("name"));
    }

    [Test]
    public void BannerName_Max255()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BannerVersionValidator.ValidateName(new string('n', 255)).IsValid, Is.True);
            Assert.That(BannerVersionValidator.ValidateName(new string('n', 256)).Errors, Contains.Key("name"));
        });
    }

    [Test]
    public void Type_IsFixedAfterCreation()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BannerVersionValidator.IsTypeChangeAllowed(BannerType.Image, BannerType.Image), Is.True);
            Assert.That(BannerVersionValidator.IsTypeChangeAllowed(BannerType.Html, BannerType.Html), Is.True);
            Assert.That(BannerVersionValidator.IsTypeChangeAllowed(BannerType.Image, BannerType.Html), Is.False);
            Assert.That(BannerVersionValidator.IsTypeChangeAllowed(BannerType.Html, BannerType.Image), Is.False);
            Assert.That((int)BannerType.Image, Is.EqualTo(0));
            Assert.That((int)BannerType.Html, Is.EqualTo(1));
        });
    }

    [Test]
    public void UnknownType_Refused()
    {
        Assert.That(BannerVersionValidator.Validate((BannerType)9, ValidImage()).Errors, Contains.Key("type"));
    }
}
