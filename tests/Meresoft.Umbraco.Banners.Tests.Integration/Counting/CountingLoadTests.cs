using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>
/// Counting under load (SC-004, SC-005): two in-process servers, each with its own buffer and flusher, share the one
/// test database. A virtual clock steps the seconds, so 60 s of traffic runs in a moment, while the load itself comes
/// from several threads and the flush of each interval runs next to the writers of the next second.
/// </summary>
[TestFixture]
[Category("Load")]
public class CountingLoadTests : BannersServiceTestBase
{
    private static readonly DateOnly Day = new(2027, 3, 1);
    private static readonly int[] ZoneIds = [1, 2, 3, 4];
    private const int FlushIntervalSeconds = 5;
    private const int WriterThreadsPerServer = 4;

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IStatisticsRepository>(sp => ActivatorUtilities.CreateInstance<StatisticsRepository>(sp));
    }

    [Test]
    public void HundredViewsPerSecondWithFourZonesForSixtySeconds_IsCountedWithLessThanTenthOfAPercentLoss()
    {
        var servers = new[] { NewServer("A"), NewServer("B") };

        // 100 views per second in total, 50 per server; a view shows 4 zones, so 400 impressions per second.
        // Every 10th view also produces a click on its first zone.
        RunSeconds(servers, seconds: 60, viewsPerSecondPerServer: 50, clickEvery: 10);

        Report("SC-004 run (100 views/s, 4 zones, 60 s, 2 servers)", servers, seconds: 60);
        AssertTotalsAndBatches(servers, seconds: 60);
    }

    [Test]
    public void ABurstOfFourHundredReportsPerSecondPerServer_IsAbsorbedWithoutLossAndWithOneWriteBatchPerInterval()
    {
        var servers = new[] { NewServer("A"), NewServer("B") };

        // 400 reports per second per server (100 views of 4 zones each), for 20 s.
        RunSeconds(servers, seconds: 20, viewsPerSecondPerServer: 100, clickEvery: 0);

        Report("Burst run (400 reports/s per server, 20 s)", servers, seconds: 20);
        AssertTotalsAndBatches(servers, seconds: 20);
        Assert.That(servers.Sum(s => s.Reported), Is.EqualTo(2L * 20 * 400));
    }

    private Server NewServer(string name)
    {
        var buffer = new CounterBuffer(Clock);
        var repository = new CountingRepository(ActivatorUtilities.CreateInstance<StatisticsRepository>(Services));
        var flusher = ActivatorUtilities.CreateInstance<CounterFlusher>(Services, buffer, (IStatisticsRepository)repository);
        return new Server(name, buffer, flusher, repository);
    }

    private void RunSeconds(Server[] servers, int seconds, int viewsPerSecondPerServer, int clickEvery)
    {
        Task[] flushes = [];
        for (int second = 1; second <= seconds; second++)
        {
            Clock.Advance(TimeSpan.FromSeconds(1));
            if (second > 1 && (second - 1) % FlushIntervalSeconds == 0)
            {
                // The flush of the interval that just ended runs next to this second's writers.
                Task.WaitAll(flushes);
                flushes = [.. servers.Select(s => Task.Run(s.FlushOnce))];
            }

            var work = new List<Task>();
            foreach (Server server in servers)
            {
                for (int t = 0; t < WriterThreadsPerServer; t++)
                {
                    int thread = t;
                    work.Add(Task.Run(() => Emit(server, viewsPerSecondPerServer, clickEvery, thread)));
                }
            }

            Task.WaitAll([.. work]);
        }

        Task.WaitAll(flushes);
        foreach (Server server in servers)
        {
            server.FlushOnce();
        }
    }

    private static void Emit(Server server, int viewsPerSecond, int clickEvery, int thread)
    {
        // The views of one second are split over the writer threads; the remainder goes to thread 0.
        int share = (viewsPerSecond / WriterThreadsPerServer) + (thread == 0 ? viewsPerSecond % WriterThreadsPerServer : 0);
        for (int i = 0; i < share; i++)
        {
            foreach (int zone in ZoneIds)
            {
                server.Buffer.AddImpression(Day, 10 + zone, zone);
                server.CountImpression();
            }

            if (clickEvery > 0 && i % clickEvery == 0)
            {
                server.Buffer.AddClick(Day, 11, 1);
                server.CountClick();
            }
        }
    }

    private List<DailyStatRow> Rows()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return [.. GetRequiredService<IStatisticsRepository>().GetDailyRows(new DateOnly(2000, 1, 1), new DateOnly(2100, 1, 1), null, null)];
    }

    private void Report(string title, Server[] servers, int seconds)
    {
        long reported = servers.Sum(s => s.Reported);
        long flushed = Rows().Sum(r => r.Impressions + r.Clicks);
        double loss = reported == 0 ? 0 : (reported - flushed) * 100.0 / reported;
        TestContext.Out.WriteLine($"{title}: reported {reported}, flushed {flushed}, loss {loss:F4} %");
        foreach (Server s in servers)
        {
            TestContext.Out.WriteLine(
                $"  server {s.Name}: reported {s.Reported}, flush runs {s.Repository.FlushRuns}, max rows written in one flush {s.Repository.MaxRowsPerFlush}, intervals {((seconds - 1) / FlushIntervalSeconds) + 1}");
        }
    }

    private void AssertTotalsAndBatches(Server[] servers, int seconds)
    {
        long reportedImpressions = servers.Sum(s => Interlocked.Read(ref s.Impressions));
        long reportedClicks = servers.Sum(s => Interlocked.Read(ref s.Clicks));
        List<DailyStatRow> rows = Rows();
        long flushedImpressions = rows.Sum(r => r.Impressions);
        long flushedClicks = rows.Sum(r => r.Clicks);

        long reported = reportedImpressions + reportedClicks;
        double lossPercent = reported == 0 ? 0 : (reported - (flushedImpressions + flushedClicks)) * 100.0 / reported;
        Assert.That(lossPercent, Is.LessThan(0.1), "loss over 0.1 %");
        Assert.That((flushedImpressions, flushedClicks), Is.EqualTo((reportedImpressions, reportedClicks)), "the flushed totals equal the reported ones");

        // One flush per server per interval (the last one is the final flush at the end of the run), and each flush
        // writes each row once, so a flush is one batch of at most one write per zone and banner.
        int intervals = ((seconds - 1) / FlushIntervalSeconds) + 1;
        foreach (Server s in servers)
        {
            Assert.That(s.Repository.FlushRuns, Is.EqualTo(intervals), $"flush runs of server {s.Name}");
            Assert.That(s.Repository.MaxRowsPerFlush, Is.LessThanOrEqualTo(ZoneIds.Length), $"rows written in one flush of server {s.Name}");
        }
    }

    private sealed class Server
    {
        public long Impressions;
        public long Clicks;

        public Server(string name, CounterBuffer buffer, CounterFlusher flusher, CountingRepository repository)
        {
            Name = name;
            Buffer = buffer;
            Flusher = flusher;
            Repository = repository;
        }

        public string Name { get; }

        public CounterBuffer Buffer { get; }

        public CounterFlusher Flusher { get; }

        public CountingRepository Repository { get; }

        public long Reported => Interlocked.Read(ref Impressions) + Interlocked.Read(ref Clicks);

        public void CountImpression() => Interlocked.Increment(ref Impressions);

        public void CountClick() => Interlocked.Increment(ref Clicks);

        public void FlushOnce()
        {
            Repository.BeginFlush();
            Flusher.Flush();
            Repository.EndFlush();
        }
    }

    /// <summary>Counts the flush runs and the rows each of them wrote, around the real repository.</summary>
    private sealed class CountingRepository : IStatisticsRepository
    {
        private readonly IStatisticsRepository _inner;
        private int _rowsInFlush;
        private int _flushRuns;
        private int _maxRows;

        public CountingRepository(IStatisticsRepository inner) => _inner = inner;

        public int FlushRuns => Volatile.Read(ref _flushRuns);

        public int MaxRowsPerFlush => Volatile.Read(ref _maxRows);

        public void BeginFlush() => Volatile.Write(ref _rowsInFlush, 0);

        public void EndFlush()
        {
            Interlocked.Increment(ref _flushRuns);
            int rows = Volatile.Read(ref _rowsInFlush);
            int max;
            do
            {
                max = Volatile.Read(ref _maxRows);
            }
            while (rows > max && Interlocked.CompareExchange(ref _maxRows, rows, max) != max);
        }

        public void AddDelta(CounterEntry delta)
        {
            Interlocked.Increment(ref _rowsInFlush);
            _inner.AddDelta(delta);
        }

        public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId) =>
            _inner.GetDailyRows(from, to, zoneId, bannerId);

        public bool Import(DateOnly day, int bannerId, int zoneId, long impressions, long clicks, bool replace) =>
            _inner.Import(day, bannerId, zoneId, impressions, clicks, replace);

        public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds) => _inner.GetTotals(bannerIds);
    }
}
