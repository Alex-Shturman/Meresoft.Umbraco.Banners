using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Configuration;

[TestFixture]
public class BannersOptionsValidatorTests
{
    private readonly BannersOptionsValidator _validator = new();

    private ValidateOptionsResult Validate(Action<BannersOptions>? change = null)
    {
        var options = new BannersOptions();
        change?.Invoke(options);
        return _validator.Validate(Options.DefaultName, options);
    }

    [Test]
    public void Defaults_MatchTheContract()
    {
        var options = new BannersOptions();

        Assert.Multiple(() =>
        {
            Assert.That(options.PublicPath, Is.EqualTo("/_banners"));
            Assert.That(options.DeliveryEndpointEnabled, Is.True);
            Assert.That(options.FlushInterval, Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(options.ViewTokenLifetime, Is.EqualTo(TimeSpan.FromMinutes(10)));
            Assert.That(options.CacheSafetyReloadInterval, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(options.Bots.AdditionalUserAgentPatterns, Is.Empty);
        });
        Assert.That(Validate().Succeeded, Is.True);
    }

    [Test]
    public void SectionName_IsMeresoftBanners()
    {
        Assert.That(BannersOptions.SectionName, Is.EqualTo("Meresoft:Banners"));
    }

    [Test]
    public void Binds_FromConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Meresoft:Banners:PublicPath"] = "/ads",
                ["Meresoft:Banners:FlushInterval"] = "00:00:30",
                ["Meresoft:Banners:Bots:AdditionalUserAgentPatterns:0"] = "my-monitoring-agent",
            })
            .Build();

        var options = configuration.GetSection(BannersOptions.SectionName).Get<BannersOptions>()!;

        Assert.Multiple(() =>
        {
            Assert.That(options.PublicPath, Is.EqualTo("/ads"));
            Assert.That(options.FlushInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.Bots.AdditionalUserAgentPatterns, Is.EqualTo(new[] { "my-monitoring-agent" }));
        });
    }

    [TestCase("/ads")]
    [TestCase("/_banners")]
    [TestCase("/a/b")]
    public void PublicPath_Valid(string path)
    {
        Assert.That(Validate(o => o.PublicPath = path).Succeeded, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("ads")]
    [TestCase("/ads/")]
    [TestCase("/")]
    [TestCase("/umbraco")]
    [TestCase("/umbraco/banners")]
    [TestCase("/Umbraco")]
    [TestCase("/a{b}")]
    [TestCase("/a}")]
    [TestCase("/ads?x=1")]
    [TestCase("/ads#top")]
    [TestCase("/ads%20x")]
    [TestCase("/a b")]
    [TestCase("/a\tb")]
    [TestCase("/a\u00a0b")]
    [TestCase("//ads")]
    [TestCase("/a//b")]
    [TestCase("/a/b/")]
    public void PublicPath_Invalid_NamesTheKey(string path)
    {
        ValidateOptionsResult result = Validate(o => o.PublicPath = path);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:PublicPath"));
    }

    [Test]
    public void PublicPath_Null_Fails()
    {
        ValidateOptionsResult result = Validate(o => o.PublicPath = null!);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:PublicPath"));
    }

    [TestCase(5)]
    [TestCase(30)]
    [TestCase(60)]
    public void FlushInterval_Valid(int seconds)
    {
        Assert.That(Validate(o => o.FlushInterval = TimeSpan.FromSeconds(seconds)).Succeeded, Is.True);
    }

    [TestCase(0)]
    [TestCase(4)]
    [TestCase(61)]
    [TestCase(-5)]
    public void FlushInterval_Invalid_NamesTheKey(int seconds)
    {
        ValidateOptionsResult result = Validate(o => o.FlushInterval = TimeSpan.FromSeconds(seconds));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:FlushInterval"));
    }

    [TestCase(1)]
    [TestCase(10)]
    [TestCase(60)]
    public void ViewTokenLifetime_Valid(int minutes)
    {
        Assert.That(Validate(o => o.ViewTokenLifetime = TimeSpan.FromMinutes(minutes)).Succeeded, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(61)]
    public void ViewTokenLifetime_Invalid_NamesTheKey(int minutes)
    {
        ValidateOptionsResult result = Validate(o => o.ViewTokenLifetime = TimeSpan.FromMinutes(minutes));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:ViewTokenLifetime"));
    }

    [Test]
    public void ViewTokenLifetime_FiftyNineSeconds_Fails()
    {
        Assert.That(Validate(o => o.ViewTokenLifetime = TimeSpan.FromSeconds(59)).Failed, Is.True);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    [TestCase(60)]
    public void CacheSafetyReloadInterval_Valid_ZeroDisables(int minutes)
    {
        Assert.That(Validate(o => o.CacheSafetyReloadInterval = TimeSpan.FromMinutes(minutes)).Succeeded, Is.True);
    }

    [TestCase(-1)]
    [TestCase(61)]
    public void CacheSafetyReloadInterval_Invalid_NamesTheKey(int minutes)
    {
        ValidateOptionsResult result = Validate(o => o.CacheSafetyReloadInterval = TimeSpan.FromMinutes(minutes));

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:CacheSafetyReloadInterval"));
    }

    [Test]
    public void CacheSafetyReloadInterval_ThirtySeconds_Fails()
    {
        Assert.That(Validate(o => o.CacheSafetyReloadInterval = TimeSpan.FromSeconds(30)).Failed, Is.True);
    }

    [Test]
    public void BotPatterns_Blank_FailsAndNamesTheKey()
    {
        ValidateOptionsResult result = Validate(o => o.Bots.AdditionalUserAgentPatterns = ["ok-agent", " "]);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:Bots:AdditionalUserAgentPatterns"));
    }

    [TestCase("foo(")]
    [TestCase("[a-")]
    [TestCase("*leading")]
    public void BotPatterns_WithRegexMetacharacters_AreAccepted(string substring)
    {
        // Patterns are case-insensitive substrings (contract), not regular expressions.
        Assert.That(Validate(o => o.Bots.AdditionalUserAgentPatterns = ["ok-agent", substring]).Succeeded, Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void BotPatterns_Empty_OrWhitespace_FailAndNameTheIndex(string blank)
    {
        ValidateOptionsResult result = Validate(o => o.Bots.AdditionalUserAgentPatterns = ["ok-agent", blank]);

        Assert.That(result.Failed, Is.True);
        Assert.That(result.FailureMessage, Does.Contain("Meresoft:Banners:Bots:AdditionalUserAgentPatterns:1"));
    }

    [Test]
    public void BotPatterns_Valid()
    {
        Assert.That(Validate(o => o.Bots.AdditionalUserAgentPatterns = ["my-monitoring-agent"]).Succeeded, Is.True);
    }

    [Test]
    public void SeveralProblems_AreAllReported()
    {
        ValidateOptionsResult result = Validate(o =>
        {
            o.PublicPath = "ads";
            o.FlushInterval = TimeSpan.FromHours(1);
        });

        Assert.That(result.Failures, Has.Count.EqualTo(2));
    }
}
