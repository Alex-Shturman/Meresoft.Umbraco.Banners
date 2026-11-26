using BenchmarkDotNet.Attributes;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Benchmarks;

/// <summary>
/// SC-003: rendering one zone with 20 active banners from the live snapshot adds less than 2 ms at p95 and runs no
/// database command. The renderer is built from the real selector, sampler, schedule filter, writer and token
/// protector; only the snapshot accessor is a stand in, and it throws if anything asks it to read the database. No
/// scope provider, repository or database exists in this graph at all, so a database command cannot run.
/// For local profiling; the p95 gate that CI enforces is the Perf test ZoneRenderPerformanceTests in the unit tests.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class ZoneRenderBenchmarks
{
    private const string Alias = "home-hero";
    private const int BannerCount = 20;

    private BannerZoneRenderer _renderer = null!;

    [GlobalSetup]
    public void Setup()
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
        var snapshot = new LiveSnapshot(
            new Dictionary<string, SnapshotZone> { [Alias] = zone },
            new HashSet<int>(),
            new HashSet<Guid>());
        var accessor = new NoDatabaseSnapshotAccessor(snapshot);

        IOptions<BannersOptions> options = Options.Create(new BannersOptions());
        var tokens = new ViewTokenProtector(new EphemeralDataProtectionProvider(), options);
        var selector = new BannerSelector(
            accessor,
            new WeightedSampler(new SharedRandomSource()),
            new ScheduleFilter(TimeProvider.System));
        _renderer = new BannerZoneRenderer(
            selector,
            accessor,
            new RequestedZoneRecorder(TimeProvider.System),
            new BannerZoneHtmlWriter(options, tokens));

        // Fail the run now, not in a measurement, when the zone does not render what the benchmark claims to measure.
        string html = RenderOnce();
        if (!html.Contains("data-mbz=\"" + Alias + "\"", StringComparison.Ordinal) || !html.Contains("<img", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The benchmark zone did not render banners; the measurement would be meaningless.");
        }
    }

    [Benchmark]
    public string RenderZone() => RenderOnce();

    private string RenderOnce()
    {
        // A fresh Items dictionary per call, as one request has: the first zone of a request also writes the script tag.
        var items = new Dictionary<object, object?>();
        ZoneSelection selection = _renderer.Select(Alias);
        return _renderer.Render(items, selection, "hero", null);
    }

    /// <summary>Serves a prebuilt snapshot. Anything that would reach the database throws.</summary>
    private sealed class NoDatabaseSnapshotAccessor : ILiveSnapshotAccessor
    {
        private readonly LiveSnapshot _snapshot;

        public NoDatabaseSnapshotAccessor(LiveSnapshot snapshot) => _snapshot = snapshot;

        public LiveSnapshot Current => _snapshot;

        public bool IsBuilt => true;

        public void MarkStartupBuildFinished()
        {
        }

        public Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null)
        {
            throw new InvalidOperationException("A database read was attempted while rendering a zone (SC-003).");
        }

        public Task ReloadLimitsAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("A database read was attempted while rendering a zone (SC-003).");
        }
    }
}
