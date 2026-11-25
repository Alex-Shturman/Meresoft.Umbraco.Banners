using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Domain;

[TestFixture]
public class ZoneValidatorTests
{
    private static BannerZone ValidZone() => new()
    {
        Alias = "home-top",
        Name = "Home top",
        Description = "Top of the invented home page",
        ShowCount = 1,
        ImageWidth = 728,
        ImageHeight = 90,
        RenderMode = ZoneRenderMode.Server,
    };

    private static FieldValidationResult Validate(Action<BannerZone>? change = null)
    {
        BannerZone zone = ValidZone();
        change?.Invoke(zone);
        return ZoneValidator.Validate(zone);
    }

    [Test]
    public void ValidZone_Passes()
    {
        Assert.That(Validate().IsValid, Is.True);
    }

    [Test]
    public void Defaults_ShowCountIsOne_RenderModeIsServer()
    {
        var zone = new BannerZone();

        Assert.Multiple(() =>
        {
            Assert.That(zone.ShowCount, Is.EqualTo(1));
            Assert.That(zone.RenderMode, Is.EqualTo(ZoneRenderMode.Server));
        });
    }

    [TestCase("a")]
    [TestCase("home-top")]
    [TestCase("side_bar2")]
    [TestCase("a0-_9")]
    public void Alias_Valid(string alias)
    {
        Assert.That(Validate(z => z.Alias = alias).IsValid, Is.True);
    }

    [Test]
    public void Alias_OfMaximumLength_IsValid()
    {
        Assert.That(Validate(z => z.Alias = "a" + new string('b', 99)).IsValid, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1top")]
    [TestCase("-top")]
    [TestCase("_top")]
    [TestCase("home top")]
    [TestCase("home.top")]
    [TestCase("homé")]
    [TestCase("home/top")]
    public void Alias_Invalid(string alias)
    {
        FieldValidationResult result = Validate(z => z.Alias = alias);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Contains.Key("alias"));
    }

    [Test]
    public void Alias_TooLong_Invalid()
    {
        Assert.That(Validate(z => z.Alias = "a" + new string('b', 100)).Errors, Contains.Key("alias"));
    }

    [Test]
    public void Alias_IsNormalisedBeforeValidation_TrimmedAndLowercased()
    {
        Assert.That(Validate(z => z.Alias = "  Home-Top  ").IsValid, Is.True);
    }

    [TestCase("  Home-Top  ", "home-top")]
    [TestCase("SIDEBAR", "sidebar")]
    [TestCase("a", "a")]
    public void AliasNormalizer_TrimsAndLowercases(string input, string expected)
    {
        Assert.That(AliasNormalizer.Normalize(input), Is.EqualTo(expected));
    }

    [Test]
    public void AliasNormalizer_Null_GivesEmpty()
    {
        Assert.That(AliasNormalizer.Normalize(null), Is.EqualTo(string.Empty));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Name_Required(string? name)
    {
        Assert.That(Validate(z => z.Name = name!).Errors, Contains.Key("name"));
    }

    [Test]
    public void Name_Max255()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Validate(z => z.Name = new string('n', 255)).IsValid, Is.True);
            Assert.That(Validate(z => z.Name = new string('n', 256)).Errors, Contains.Key("name"));
        });
    }

    [Test]
    public void Description_Nullable_Max1000()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Validate(z => z.Description = null).IsValid, Is.True);
            Assert.That(Validate(z => z.Description = new string('d', 1000)).IsValid, Is.True);
            Assert.That(Validate(z => z.Description = new string('d', 1001)).Errors, Contains.Key("description"));
        });
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(10)]
    public void ShowCount_Valid(int value)
    {
        Assert.That(Validate(z => z.ShowCount = value).IsValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(11)]
    public void ShowCount_Invalid(int value)
    {
        Assert.That(Validate(z => z.ShowCount = value).Errors, Contains.Key("showCount"));
    }

    [TestCase(null)]
    [TestCase(1)]
    [TestCase(4000)]
    public void ImageWidth_Valid(int? value)
    {
        Assert.That(Validate(z => z.ImageWidth = value).IsValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-3)]
    [TestCase(4001)]
    public void ImageWidth_Invalid(int value)
    {
        Assert.That(Validate(z => z.ImageWidth = value).Errors, Contains.Key("imageWidth"));
    }

    [TestCase(null)]
    [TestCase(1)]
    [TestCase(4000)]
    public void ImageHeight_Valid(int? value)
    {
        Assert.That(Validate(z => z.ImageHeight = value).IsValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(4001)]
    public void ImageHeight_Invalid(int value)
    {
        Assert.That(Validate(z => z.ImageHeight = value).Errors, Contains.Key("imageHeight"));
    }

    [Test]
    public void RenderMode_OnlyServerAndClient()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Validate(z => z.RenderMode = ZoneRenderMode.Server).IsValid, Is.True);
            Assert.That(Validate(z => z.RenderMode = ZoneRenderMode.Client).IsValid, Is.True);
            Assert.That(Validate(z => z.RenderMode = (ZoneRenderMode)2).Errors, Contains.Key("renderMode"));
            Assert.That((int)ZoneRenderMode.Server, Is.EqualTo(0));
            Assert.That((int)ZoneRenderMode.Client, Is.EqualTo(1));
        });
    }

    [Test]
    public void SeveralProblems_AreAllReported()
    {
        FieldValidationResult result = Validate(z =>
        {
            z.Alias = "";
            z.Name = "";
            z.ShowCount = 0;
        });

        Assert.That(result.Errors.Keys, Is.SupersetOf(new[] { "alias", "name", "showCount" }));
    }
}
