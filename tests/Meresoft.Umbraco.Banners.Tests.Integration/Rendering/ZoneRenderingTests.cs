using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Encodings.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Rendering;

[TestFixture]
public class ZoneRenderingTests : BannersServiceTestBase
{
    private Banner _banner = null!;

    private async Task PublishBannerInZoneAsync(string zoneAlias = "home-top")
    {
        BannerZone zone = await NewZoneAsync(zoneAlias);
        _banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        var published = await BannerService.PublishAsync(_banner.Key, User);
        Assert.That(published.Success, Is.True, published.Status.ToString());
        await WaitForSnapshotAsync(s => s.Zones.TryGetValue(zoneAlias, out SnapshotZone? z) && z.Candidates.Count > 0);
    }

    private async Task WaitForSnapshotAsync(Func<LiveSnapshot, bool> condition)
    {
        ILiveSnapshotAccessor accessor = GetRequiredService<ILiveSnapshotAccessor>();
        DateTime stop = DateTime.UtcNow.AddSeconds(5);
        while (!condition(accessor.Current) && DateTime.UtcNow < stop)
        {
            await Task.Delay(50);
        }

        Assert.That(condition(accessor.Current), Is.True, "the snapshot did not change within 5 seconds");
    }

    private static ViewContext NewViewContext(DefaultHttpContext http) =>
        new() { HttpContext = http };

    private async Task<string> RenderTagHelperAsync(DefaultHttpContext http, string alias, string? cssClass = null, string? fallback = null, Action? childContentExecuted = null)
    {
        BannerZoneTagHelper helper = ActivatorUtilities.CreateInstance<BannerZoneTagHelper>(Services);
        helper.ViewContext = NewViewContext(http);
        helper.Alias = alias;
        helper.CssClass = cssClass;

        var context = new TagHelperContext("banner-zone", [], new Dictionary<object, object>(), Guid.NewGuid().ToString());
        var output = new TagHelperOutput(
            "banner-zone",
            [],
            (_, _) =>
            {
                childContentExecuted?.Invoke();
                var content = new DefaultTagHelperContent();
                if (fallback is not null)
                {
                    content.SetHtmlContent(fallback);
                }

                return Task.FromResult<TagHelperContent>(content);
            });

        await helper.ProcessAsync(context, output);

        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Test]
    public async Task TagHelper_RendersThePublishedBanner()
    {
        await PublishBannerInZoneAsync();

        string html = await RenderTagHelperAsync(new DefaultHttpContext(), "Home-Top", "my-ads");

        Assert.That(html, Does.Contain("class=\"mb-zone my-ads\""));
        Assert.That(html, Does.Contain($"data-mbb=\"{_banner.Id}\""));
        Assert.That(html, Does.Contain($"href=\"/_banners/c/{_banner.Id}?z={_banner.Draft!.ZoneIds[0]}\""), "the link goes through the click endpoint");
        Assert.That(html, Does.Contain("alt=\"Invented summer promo\""));
        Assert.That(html, Does.Contain("src=\"/media/invented/banner.png"), "a site relative address from the real media URL provider");
        Assert.That(html, Does.Not.Contain("localhost"));
    }

    [Test]
    public async Task TagHelper_DoesNotRunItsChildContent_WhenABannerIsShown()
    {
        await PublishBannerInZoneAsync();
        int executions = 0;

        string html = await RenderTagHelperAsync(new DefaultHttpContext(), "home-top", fallback: "<p>Fallback</p>", childContentExecuted: () => executions++);

        Assert.That(html, Does.Contain($"data-mbb=\"{_banner.Id}\""));
        Assert.That(executions, Is.EqualTo(0));
    }

    [Test]
    public async Task TagHelper_RunsItsChildContent_WhenThereIsNoBanner_OrNoZone()
    {
        await NewZoneAsync("empty-zone");
        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();
        int executions = 0;

        await RenderTagHelperAsync(new DefaultHttpContext(), "empty-zone", fallback: "<p>Fallback</p>", childContentExecuted: () => executions++);
        await RenderTagHelperAsync(new DefaultHttpContext(), "no-such-zone", fallback: "<p>Fallback</p>", childContentExecuted: () => executions++);

        Assert.That(executions, Is.EqualTo(2));
    }

    [Test]
    public async Task APublishedHtmlBanner_IsRenderedAsStored_InsideTheHtmlBox()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        var banner = new Banner
        {
            Key = Guid.NewGuid(),
            Name = "Html offer",
            Type = BannerType.Html,
            CreateDate = Clock.GetUtcNow().UtcDateTime,
            UpdateDate = Clock.GetUtcNow().UtcDateTime,
            CreatedBy = User,
            UpdatedBy = User,
            Draft = new BannerVersion { Kind = BannerVersionKind.Draft, Html = "<script>invented()</script>", Weight = 1, SavedDate = Clock.GetUtcNow().UtcDateTime, SavedBy = User, ZoneIds = [zone.Id] },
        };
        using (IScope scope = ScopeProvider.CreateScope())
        {
            var repository = GetRequiredService<Meresoft.Umbraco.Banners.Persistence.Repositories.IBannerRepository>();
            repository.Add(banner);
            repository.CopyDraftOverLive(banner.Id, Clock.GetUtcNow().UtcDateTime);
            scope.Complete();
        }

        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();
        string html = await RenderTagHelperAsync(new DefaultHttpContext(), "home-top");

        Assert.That(html, Does.Contain("<div class=\"mb-banner mb-html\" data-mbb=\"" + banner.Id + "\"><script>invented()</script></div>"));
        Assert.That(html, Does.Not.Contain("<a href"));
    }

    [Test]
    public async Task ViewComponent_RendersThePublishedBanner()
    {
        await PublishBannerInZoneAsync();
        BannerZoneViewComponent component = ActivatorUtilities.CreateInstance<BannerZoneViewComponent>(Services);

        IViewComponentResult result = component.Invoke("home-top", "my-ads");

        using var writer = new StringWriter();
        ((HtmlContentViewComponentResult)result).EncodedContent.WriteTo(writer, HtmlEncoder.Default);
        Assert.That(writer.ToString(), Does.Contain($"data-mbb=\"{_banner.Id}\""));
    }

    [Test]
    public async Task Render_RunsNoDatabaseCommand()
    {
        await PublishBannerInZoneAsync();

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IUmbracoDatabase database = scope.Database;
        database.EnableSqlCount = true;
        int before = database.SqlCount;

        string tag = await RenderTagHelperAsync(new DefaultHttpContext(), "home-top");
        string unknown = await RenderTagHelperAsync(new DefaultHttpContext(), "not-there");
        _ = ActivatorUtilities.CreateInstance<BannerZoneViewComponent>(Services).Invoke("home-top", null);

        Assert.That(tag, Is.Not.Empty);
        Assert.That(unknown, Is.Empty);
        Assert.That(database.SqlCount - before, Is.EqualTo(0), "SC-003: no database command while rendering");
    }

    [Test]
    public async Task TwoZonesWithTheSameAlias_RenderIndependently_AndTheScriptIsWrittenOnce()
    {
        await PublishBannerInZoneAsync();
        var http = new DefaultHttpContext();

        string first = await RenderTagHelperAsync(http, "home-top");
        string second = await RenderTagHelperAsync(http, "home-top");

        Assert.That(first, Does.Contain($"data-mbb=\"{_banner.Id}\""));
        Assert.That(second, Does.Contain($"data-mbb=\"{_banner.Id}\""));
        Assert.That(first, Does.Contain("<script"));
        Assert.That(second, Does.Not.Contain("<script"));
    }

    [Test]
    public async Task UnknownAlias_RendersNothing_AndIsRecordedAsRequested()
    {
        string html = await RenderTagHelperAsync(new DefaultHttpContext(), "Brand-New");

        Assert.That(html, Is.Empty);
        Assert.That(GetRequiredService<RequestedZoneRecorder>().Snapshot().Select(r => r.Alias), Does.Contain("brand-new"));
    }

    [Test]
    public async Task ZoneWithoutActiveBanner_RendersTheFallback()
    {
        await NewZoneAsync("empty-zone");
        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();

        string html = await RenderTagHelperAsync(new DefaultHttpContext(), "empty-zone", fallback: "<p>Invented fallback</p>");

        Assert.That(html, Is.EqualTo("<p>Invented fallback</p>"));
    }

    [Test]
    public async Task FlushJob_WritesRequestedZones_WithFirstAndLastSeen_AndSkipsExistingZones()
    {
        await NewZoneAsync("exists-already");
        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();
        await RenderTagHelperAsync(new DefaultHttpContext(), "needs-setup");
        Clock.Advance(TimeSpan.FromMinutes(2));
        await RenderTagHelperAsync(new DefaultHttpContext(), "needs-setup");
        RequestedZoneRecorder recorder = GetRequiredService<RequestedZoneRecorder>();
        recorder.Record("exists-already");

        await ActivatorUtilities.CreateInstance<BannersFlushJob>(Services).RunJobAsync(CancellationToken.None);

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        List<RequestedZoneDto> rows = scope.Database.Fetch<RequestedZoneDto>();
        Assert.That(rows.Select(r => r.Alias), Is.EqualTo(new[] { "needs-setup" }));
        Assert.That(rows[0].LastSeen, Is.GreaterThan(rows[0].FirstSeen));
        Assert.That(DateTime.SpecifyKind(rows[0].FirstSeen, DateTimeKind.Utc), Is.EqualTo(new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task FlushJob_KeepsFirstSeen_WhenTheAliasWasRecordedBefore()
    {
        await GetRequiredService<ILiveSnapshotAccessor>().RebuildAsync();
        await RenderTagHelperAsync(new DefaultHttpContext(), "needs-setup");
        BannersFlushJob job = ActivatorUtilities.CreateInstance<BannersFlushJob>(Services);
        await job.RunJobAsync(CancellationToken.None);
        Clock.Advance(TimeSpan.FromHours(1));
        await RenderTagHelperAsync(new DefaultHttpContext(), "needs-setup");

        await job.RunJobAsync(CancellationToken.None);

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        RequestedZoneDto row = scope.Database.Fetch<RequestedZoneDto>().Single();
        Assert.That(row.LastSeen - row.FirstSeen, Is.EqualTo(TimeSpan.FromHours(1)));
    }
}
