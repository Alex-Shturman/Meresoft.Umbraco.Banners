using System.Diagnostics;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Rendering;

/// <summary>
/// SC-003 as a gate: one zone with 20 active banners renders in under 2 ms at the 95th percentile, measured per call with
/// a stopwatch over 100,000 calls, and with no database access at all (the renderer is built from real parts, the only
/// stand-in is a snapshot accessor that throws on anything that would read the database). The BenchmarkDotNet class in
/// the Benchmarks project measures the same render for local profiling; this test is what CI enforces.
/// </summary>
[TestFixture]
[Category("Perf")]
public class ZoneRenderPerformanceTests
{
    private const string Alias = "home-hero";
    private const int BannerCount = 20;
    private const int WarmUpCalls = 5_000;
    private const int MeasuredCalls = 100_000;
    private const double P95LimitMilliseconds = 2.0;

    [Test]
    public void RenderingAZoneWithTwentyBanners_HasAP95UnderTwoMilliseconds_AndNeverTouchesTheDatabase()
    {
        BannerZoneRenderer renderer = BuildRenderer();
        for (int i = 0; i < WarmUpCalls; i++)
        {
            RenderOnce(renderer);
        }

        var ticks = new long[MeasuredCalls];
        for (int i = 0; i < MeasuredCalls; i++)
        {
            long start = Stopwatch.GetTimestamp();
            RenderOnce(renderer);
            ticks[i] = Stopwatch.GetTimestamp() - start;
        }

        Array.Sort(ticks);
        double toMilliseconds = 1000.0 / Stopwatch.Frequency;
        double p95 = ticks[(int)Math.Ceiling(MeasuredCalls * 0.95) - 1] * toMilliseconds;
        double max = ticks[^1] * toMilliseconds;
        TestContext.Out.WriteLine($"SC-003 zone render, {MeasuredCalls} calls: p95 = {p95:F4} ms, median = {ticks[MeasuredCalls / 2] * toMilliseconds:F4} ms, max = {max:F3} ms, limit {P95LimitMilliseconds:F1} ms");

        Assert.That(p95, Is.LessThan(P95LimitMilliseconds), "p95 of one zone render");
    }

    private static string RenderOnce(BannerZoneRenderer renderer)
    {
        // A fresh Items dictionary per call, as one request has: the first zone of a request also writes the script tag.
        ZoneSelection selection = renderer.Select(Alias);
        return renderer.Render(new Dictionary<object, object?>(), selection, "hero", null);
    }

    private static BannerZoneRenderer BuildRenderer()
    {
        var banners = new List<SnapshotBanner>();
        for (int i = 1; i <= BannerCount; i++)
        {
            banners.Add(new SnapshotBanner
            {
                BannerId = i,
                ZoneId = 1,
                ImageUrl = $"/media/sample/banner-{i}.jpg",
                Width = 728,
                Height = 90,
                AltText = $"Sample banner {i}",
                Title = $"Banner {i}",
                LinkUrl = $"https://example.test/offer/{i}",
                OpenInNewWindow = i % 2 == 0,
                Weight = 1 + (i % 5),
                StartUtc = DateTime.UtcNow.AddDays(-30),
                EndUtc = DateTime.UtcNow.AddDays(30),
            });
        }

        var zone = new SnapshotZone(1, Alias, 3, ZoneRenderMode.Server, 728, 90, banners);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { [Alias] = zone }, new HashSet<int>(), new HashSet<Guid>());
        var accessor = new NoDatabaseSnapshotAccessor(snapshot);

        IOptions<BannersOptions> options = Options.Create(new BannersOptions());
        var tokens = new ViewTokenProtector(new EphemeralDataProtectionProvider(), options);
        var selector = new BannerSelector(accessor, new WeightedSampler(new SharedRandomSource()), new ScheduleFilter(TimeProvider.System));
        var renderer = new BannerZoneRenderer(selector, accessor, new RequestedZoneRecorder(TimeProvider.System), new BannerZoneHtmlWriter(options, tokens));

        // The gate must measure a real render, not an empty zone.
        string html = RenderOnce(renderer);
        Assert.That(html, Does.Contain("data-mbz=\"" + Alias + "\"").And.Contain("<img"));
        return renderer;
    }

    /// <summary>Serves a prebuilt snapshot. The two members that read the database throw.</summary>
    private sealed class NoDatabaseSnapshotAccessor : ILiveSnapshotAccessor
    {
        private readonly LiveSnapshot _snapshot;

        public NoDatabaseSnapshotAccessor(LiveSnapshot snapshot) => _snapshot = snapshot;

        public LiveSnapshot Current => _snapshot;

        public bool IsBuilt => true;

        public void MarkStartupBuildFinished()
        {
        }

        public Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null) =>
            throw new InvalidOperationException("A database read was attempted while rendering a zone (SC-003).");

        public Task ReloadLimitsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("A database read was attempted while rendering a zone (SC-003).");
    }
}
