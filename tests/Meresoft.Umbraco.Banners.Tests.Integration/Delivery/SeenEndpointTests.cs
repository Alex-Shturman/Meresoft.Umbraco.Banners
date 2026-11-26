using System.Net;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

[TestFixture]
public class SeenEndpointTests : PublicEndpointTestBase
{
    private const string SeenPath = "/_banners/seen";

    private async Task<HttpResponseMessage> SeenAsync(string body, string? userAgent = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, SeenPath) { Content = PlainText(body) };
        if (userAgent is not null)
        {
            request.Headers.UserAgent.Clear();
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        }

        HttpResponseMessage response = await Anonymous.SendAsync(request);
        return response;
    }

    private async Task AssertAcceptedAsync(string body, string? userAgent = null)
    {
        HttpResponseMessage response = await SeenAsync(body, userAgent);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        AssertPublicHeaders(response);
    }

    [Test]
    public async Task AValidLine_CountsOneImpression_ForTodayTheBannerAndTheTokenZone()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string token = IssueToken(banner.ZoneId, banner.BannerId);

        await AssertAcceptedAsync($"{token} {banner.BannerId}");

        Assert.That(Buffer.Swap(), Is.EqualTo(new[] { new CounterEntry(Today, banner.BannerId, banner.ZoneId, 1, 0) }));
    }

    [Test]
    public async Task OneBannerShownInTwoZonesOfOnePage_AndSeenInBoth_CountsOneImpressionInEachZone()
    {
        PublishedBanner first = await PublishBannerAsync("home-top");
        (Guid secondZoneKey, _) = await CreateZoneAsync("sidebar");
        int secondZoneId = (await GetRequiredService<Meresoft.Umbraco.Banners.Services.IBannerZoneService>().GetAsync(secondZoneKey))!.Id;
        string tokenA = IssueToken(first.ZoneId, first.BannerId);
        string tokenB = IssueToken(secondZoneId, first.BannerId);

        await AssertAcceptedAsync($"{tokenA} {first.BannerId}\n{tokenB} {first.BannerId}");

        IReadOnlyList<CounterEntry> counted = Buffer.Swap();
        Assert.That(counted, Has.Count.EqualTo(2));
        Assert.That(counted.Select(c => (c.ZoneId, c.Impressions)), Is.EquivalentTo(new[] { (first.ZoneId, 1L), (secondZoneId, 1L) }));
    }

    [Test]
    public async Task ARepeatedLine_CountsOnce_EvenInAnotherRequest()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string line = $"{IssueToken(banner.ZoneId, banner.BannerId)} {banner.BannerId}";

        await AssertAcceptedAsync(line + "\n" + line);
        await AssertAcceptedAsync(line);

        Assert.That(Buffer.Swap().Sum(c => c.Impressions), Is.EqualTo(1));
    }

    [Test]
    public async Task ABannerIdNotInTheToken_CountsNothing_AndStillAnswers204()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string token = IssueToken(banner.ZoneId, banner.BannerId);

        await AssertAcceptedAsync($"{token} {banner.BannerId + 1000}");

        Assert.That(Buffer.Swap(), Is.Empty);
    }

    [Test]
    public async Task ATamperedToken_CountsNothing()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string token = IssueToken(banner.ZoneId, banner.BannerId);
        string tampered = token[..^3] + (token.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA");

        await AssertAcceptedAsync($"{tampered} {banner.BannerId}");

        Assert.That(Buffer.Swap(), Is.Empty);
    }

    [Test]
    public async Task AnExpiredToken_CountsNothing()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        var shortLived = new Meresoft.Umbraco.Banners.Tokens.ViewTokenProtector(
            GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(),
            Microsoft.Extensions.Options.Options.Create(new Meresoft.Umbraco.Banners.Configuration.BannersOptions { ViewTokenLifetime = TimeSpan.FromMilliseconds(1) }));
        string token = shortLived.Issue(banner.ZoneId, [banner.BannerId]);
        await Task.Delay(150);

        await AssertAcceptedAsync($"{token} {banner.BannerId}");

        Assert.That(Buffer.Swap(), Is.Empty);
    }

    [Test]
    public async Task ABot_CountsNothing_AndStillAnswers204()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string token = IssueToken(banner.ZoneId, banner.BannerId);

        await AssertAcceptedAsync($"{token} {banner.BannerId}", "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)");

        Assert.That(Buffer.Swap(), Is.Empty);
    }

    [TestCase("")]
    [TestCase("garbage")]
    [TestCase("a b c")]
    public async Task AnyBody_Answers204(string body)
    {
        await AssertAcceptedAsync(body);
    }

    [Test]
    public async Task ABodyAbove16Kb_Gets413()
    {
        HttpResponseMessage response = await SeenAsync(new string('a', 16 * 1024 + 1));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
        AssertPublicHeaders(response);
    }

    [Test]
    public async Task ABodyOfExactly16Kb_IsAccepted()
    {
        await AssertAcceptedAsync(new string('a', 16 * 1024));
    }

    [TestCase("GET")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public async Task OtherMethods_Get405(string method)
    {
        HttpResponseMessage response = await Anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), SeenPath));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
        AssertPublicHeaders(response);
    }
}
