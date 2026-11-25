using System.Net;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Base of the public endpoint tests (click, seen): an anonymous client that does not follow redirects, a way to
/// publish an invented banner, and a look at the in-memory counters (the flush job does not run in tests).
/// </summary>
public abstract class PublicEndpointTestBase : BannersApiTestBase
{
    protected const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
    protected const string StoredLink = "https://example.org/show";

    protected HttpClient Anonymous { get; private set; } = null!;

    protected CounterBuffer Buffer => GetRequiredService<CounterBuffer>();

    protected DateOnly Today => DateOnly.FromDateTime(GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);

    [SetUp]
    public void CreateAnonymousClient()
    {
        Anonymous = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Anonymous.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserAgent);
        Buffer.Swap();
    }

    protected sealed record PublishedBanner(int BannerId, Guid BannerKey, int ZoneId, Guid ZoneKey);

    /// <summary>Creates the zone, a banner assigned to it, publishes it and waits until the page snapshot shows it.</summary>
    protected async Task<PublishedBanner> PublishBannerAsync(string zoneAlias)
    {
        (Guid zoneKey, _) = await CreateZoneAsync(zoneAlias);
        Guid bannerKey = await CreateBannerAsync(NewMediaKey(), zoneKey);
        HttpResponseMessage published = await PublishAsync(bannerKey);
        Assert.That(published.StatusCode, Is.EqualTo(HttpStatusCode.OK), await published.Content.ReadAsStringAsync());

        Banner banner = (await GetRequiredService<IBannerService>().GetAsync(bannerKey))!;
        BannerZone zone = (await GetRequiredService<IBannerZoneService>().GetAsync(zoneKey))!;
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue(zoneAlias, out SnapshotZone? z) && z.Candidates.Any(c => c.BannerId == banner.Id));
        return new PublishedBanner(banner.Id, bannerKey, zone.Id, zoneKey);
    }

    protected async Task WaitForSnapshotAsync(Func<LiveSnapshot, bool> condition)
    {
        ILiveSnapshotAccessor accessor = GetRequiredService<ILiveSnapshotAccessor>();
        DateTime stop = DateTime.UtcNow.AddSeconds(5);
        while (!condition(accessor.Current) && DateTime.UtcNow < stop)
        {
            await Task.Delay(50);
        }

        Assert.That(condition(accessor.Current), Is.True, "the snapshot did not change within 5 seconds");
    }

    protected static void AssertPublicHeaders(HttpResponseMessage response)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True, "Cache-Control: no-store");
            Assert.That(response.Headers.GetValues("X-Robots-Tag"), Is.EqualTo(new[] { "noindex, nofollow" }));
            Assert.That(response.Headers.Contains("Set-Cookie"), Is.False, "no cookie");
        });
    }

    /// <summary>A token as a rendered zone would carry it.</summary>
    protected string IssueToken(int zoneId, params int[] bannerIds) =>
        GetRequiredService<IViewTokenProtector>().Issue(zoneId, bannerIds);

    protected static StringContent PlainText(string body) => new(body, System.Text.Encoding.UTF8, "text/plain");
}
