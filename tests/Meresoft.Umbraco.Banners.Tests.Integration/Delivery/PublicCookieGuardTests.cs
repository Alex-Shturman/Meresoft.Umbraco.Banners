using System.Net;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Delivery;

/// <summary>
/// Review 4: the public endpoints never answer with a cookie, whatever other middleware of the site does. A test
/// middleware sets a cookie for every request in OnStarting, the way a session or consent component would.
/// </summary>
[TestFixture]
public class PublicCookieGuardTests : PublicEndpointTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddTransient<IStartupFilter, CookieSettingStartupFilter>();
    }

    [Test]
    public async Task ACookieSetByOtherMiddleware_IsRemovedFromTheClickAnswer()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");

        HttpResponseMessage response = await Anonymous.GetAsync($"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Contains("Set-Cookie"), Is.False);
    }

    [Test]
    public async Task ACookieSetByOtherMiddleware_IsRemovedFromEveryOtherPublicAnswer()
    {
        foreach ((HttpMethod method, string path) in new[] { (HttpMethod.Post, "/_banners/seen"), (HttpMethod.Get, "/_banners/seen"), (HttpMethod.Get, "/_banners/c/99999?z=1"), (HttpMethod.Get, "/_banners/c/abc"), (HttpMethod.Get, "/_banners/zone/some-zone"), (HttpMethod.Get, "/_banners/zone/9-not-valid") })
        {
            HttpResponseMessage response = await Anonymous.SendAsync(new HttpRequestMessage(method, path) { Content = method == HttpMethod.Post ? PlainText("x y") : null });

            Assert.That(response.Headers.Contains("Set-Cookie"), Is.False, $"{method} {path}");
        }
    }

    [Test]
    public async Task OtherPathsKeepTheirCookies_TheGuardIsScopedToThePublicPath()
    {
        HttpResponseMessage response = await Anonymous.GetAsync("/umbraco/management/api/v1/meresoft-banners/zones");

        Assert.That(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies) && cookies.Any(c => c.StartsWith("tracker=", StringComparison.Ordinal)), Is.True);
    }

    private sealed class CookieSettingStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Append("Set-Cookie", "tracker=1; path=/");
                    return Task.CompletedTask;
                });
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
