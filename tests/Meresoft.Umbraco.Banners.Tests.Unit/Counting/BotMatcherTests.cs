using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Counting;

[TestFixture]
public class BotMatcherTests
{
    private static BotMatcher Create(params string[] additional) =>
        new(Options.Create(new BannersOptions { Bots = new BotOptions { AdditionalUserAgentPatterns = additional } }));

    [TestCase("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", Description = "crawler")]
    [TestCase("Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)", Description = "crawler")]
    [TestCase("Mozilla/5.0 (compatible; YandexBot/3.0; +http://yandex.com/bots)", Description = "crawler")]
    [TestCase("Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)", Description = "crawler")]
    [TestCase("Mozilla/5.0 (compatible; Baiduspider/2.0; +http://www.baidu.com/search/spider.html)", Description = "crawler")]
    [TestCase("Mozilla/5.0 (compatible; GPTBot/1.0; +https://openai.com/gptbot)", Description = "crawler")]
    [TestCase("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)", Description = "link previewer")]
    [TestCase("Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)", Description = "link previewer")]
    [TestCase("WhatsApp/2.23.20 A", Description = "link previewer")]
    [TestCase("TelegramBot (like TwitterBot)", Description = "link previewer")]
    [TestCase("Mozilla/5.0 (compatible; UptimeRobot/2.0; http://www.uptimerobot.com/)", Description = "uptime monitor")]
    [TestCase("Pingdom.com_bot_version_1.4_(http://www.pingdom.com/)", Description = "uptime monitor")]
    [TestCase("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/120.0.0.0 Safari/537.36", Description = "headless browser")]
    [TestCase("Mozilla/5.0 (Unknown; Linux x86_64) AppleWebKit/538.1 (KHTML, like Gecko) PhantomJS/2.1.1 Safari/538.1", Description = "headless browser")]
    [TestCase("curl/8.4.0", Description = "command line client")]
    [TestCase("python-requests/2.31.0", Description = "script client")]
    [TestCase("", Description = "empty")]
    [TestCase("   ", Description = "blank")]
    [TestCase(null, Description = "missing")]
    public void KnownAutomatedClients_AreBots(string? userAgent)
    {
        Assert.That(Create().IsBot(userAgent), Is.True);
    }

    [TestCase("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")]
    [TestCase("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15")]
    [TestCase("Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0")]
    [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Mobile/15E148 Safari/604.1")]
    [TestCase("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36")]
    [TestCase("Mozilla/5.0 (Linux; Android 12; CUBOT_KINGKONG_5 Build/SP1A) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36", Description = "a phone brand that contains the letters bot")]
    [TestCase("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0")]
    public void CurrentBrowsers_AreNotBots(string userAgent)
    {
        Assert.That(Create().IsBot(userAgent), Is.False);
    }

    [Test]
    public void AdditionalPatterns_AreCaseInsensitiveSubstrings()
    {
        BotMatcher matcher = Create("Acme-Monitor", "internal.scan");

        Assert.That(matcher.IsBot("Mozilla/5.0 (acme-MONITOR/3.1) AppleWebKit/537.36 Chrome/131.0 Safari/537.36"), Is.True);
        Assert.That(matcher.IsBot("tool INTERNAL.SCAN v2"), Is.True);
        Assert.That(matcher.IsBot("Mozilla/5.0 Chrome/131.0 Safari/537.36"), Is.False);
    }

    [Test]
    public void APatternIsPlainText_NotARegularExpression()
    {
        BotMatcher matcher = Create("a.c", "x+");

        Assert.That(matcher.IsBot("Mozilla/5.0 Chrome/131.0 abc"), Is.False, "the dot is not a wildcard");
        Assert.That(matcher.IsBot("Mozilla/5.0 Chrome/131.0 a.c"), Is.True);
        Assert.That(matcher.IsBot("Mozilla/5.0 Chrome/131.0 xxx"), Is.False, "the plus is not a repeat");
    }

    [Test]
    public void BlankAdditionalPatterns_AreIgnored_SoTheyCannotMatchEveryAgent()
    {
        BotMatcher matcher = Create("", "  ");

        Assert.That(matcher.IsBot("Mozilla/5.0 Chrome/131.0 Safari/537.36"), Is.False);
    }
}
