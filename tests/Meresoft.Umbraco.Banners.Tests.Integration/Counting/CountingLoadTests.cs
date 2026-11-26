using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Data;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.Scoping;
using IScope = Umbraco.Cms.Infrastructure.Scoping.IScope;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>
/// Counting under load (SC-004, SC-005): two in-process servers, each with its own buffer and flusher, share the one
/// test database. A virtual clock steps the seconds, so 60 s of traffic runs in a moment, while the load itself comes
/// from several threads and the flush of each interval runs next to the writers of the next second.
/// Scope of the write check: the buffer and flusher level. Every database write scope of a server goes through a
/// counting scope provider, so the test counts real write transactions (not calls it makes itself) and proves that
/// none opens while views are only being reported, and that all the writes of an interval belong to one flush and
/// stay within one transaction per counted row. What it does not drive is the timer of <c>BannersFlushJob</c>, which
/// belongs to Umbraco's background job runner; the test calls the flush at each interval boundary of its virtual clock.
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
        var scopes = new CountingScopeProvider(GetRequiredService<IScopeProvider>());
        var flusher = ActivatorUtilities.CreateInstance<CounterFlusher>(Services, buffer, (IScopeProvider)scopes);
        return new Server(name, buffer, flusher, scopes);
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
                $"  server {s.Name}: reported {s.Reported}, flush windows {s.Scopes.Windows.Count}, write transactions {s.Scopes.WriteTransactions}, outside a flush {s.Scopes.WritesOutsideAFlush}, most in one flush {s.Scopes.Windows.Max()}, intervals {((seconds - 1) / FlushIntervalSeconds) + 1}");
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

        // Real write transactions, counted by the scope provider the flusher uses: none outside a flush window, one
        // flush window per interval (the last window is the final flush at the end of the run), and a window opens at
        // most one transaction per counted row (one zone and banner pair each).
        int intervals = ((seconds - 1) / FlushIntervalSeconds) + 1;
        foreach (Server s in servers)
        {
            Assert.That(s.Scopes.WritesOutsideAFlush, Is.EqualTo(0), $"write transactions of server {s.Name} outside a flush");
            Assert.That(s.Scopes.Windows, Has.Count.EqualTo(intervals), $"flush windows of server {s.Name}");
            Assert.That(s.Scopes.Windows.Max(), Is.LessThanOrEqualTo(ZoneIds.Length), $"write transactions in one flush of server {s.Name}");
        }
    }

    private sealed class Server
    {
        public long Impressions;
        public long Clicks;

        public Server(string name, CounterBuffer buffer, CounterFlusher flusher, CountingScopeProvider scopes)
        {
            Name = name;
            Buffer = buffer;
            Flusher = flusher;
            Scopes = scopes;
        }

        public string Name { get; }

        public CounterBuffer Buffer { get; }

        public CounterFlusher Flusher { get; }

        public CountingScopeProvider Scopes { get; }

        public long Reported => Interlocked.Read(ref Impressions) + Interlocked.Read(ref Clicks);

        public void CountImpression() => Interlocked.Increment(ref Impressions);

        public void CountClick() => Interlocked.Increment(ref Clicks);

        public void FlushOnce()
        {
            Scopes.BeginWindow();
            try
            {
                Flusher.Flush();
            }
            finally
            {
                Scopes.EndWindow();
            }
        }
    }

    /// <summary>
    /// Wraps the real scope provider and counts the scopes that are meant to write (not auto completing). A scope created
    /// on the thread that is inside a flush window belongs to that window; any other is a write outside a flush.
    /// </summary>
    private sealed class CountingScopeProvider : IScopeProvider
    {
        [ThreadStatic]
        private static int _currentWindow;

        private readonly IScopeProvider _inner;
        private readonly object _gate = new();
        private readonly List<int> _windows = [];
        private int _outside;

        public CountingScopeProvider(IScopeProvider inner) => _inner = inner;

        public IScopeContext? Context => _inner.Context;

        public global::Umbraco.Cms.Infrastructure.Persistence.ISqlContext SqlContext => _inner.SqlContext;

        /// <summary>Write transactions per flush window, in order.</summary>
        public IReadOnlyList<int> Windows
        {
            get
            {
                lock (_gate)
                {
                    return [.. _windows];
                }
            }
        }

        public int WriteTransactions
        {
            get
            {
                lock (_gate)
                {
                    return _windows.Sum() + _outside;
                }
            }
        }

        public int WritesOutsideAFlush
        {
            get
            {
                lock (_gate)
                {
                    return _outside;
                }
            }
        }

        public void BeginWindow()
        {
            lock (_gate)
            {
                _windows.Add(0);
                _currentWindow = _windows.Count;
            }
        }

        public void EndWindow() => _currentWindow = 0;

        private void CountWrite()
        {
            lock (_gate)
            {
                if (_currentWindow > 0)
                {
                    _windows[_currentWindow - 1]++;
                }
                else
                {
                    _outside++;
                }
            }
        }

        public IScope CreateScope(
            IsolationLevel isolationLevel = IsolationLevel.Unspecified,
            RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
            IEventDispatcher? eventDispatcher = null,
            IScopedNotificationPublisher? scopedNotificationPublisher = null,
            bool? scopeFileSystems = null,
            bool callContext = false,
            bool autoComplete = false)
        {
            if (!autoComplete)
            {
                CountWrite();
            }

            return _inner.CreateScope(isolationLevel, repositoryCacheMode, eventDispatcher, scopedNotificationPublisher, scopeFileSystems, callContext, autoComplete);
        }

        public ICoreScope CreateCoreScope(
            IsolationLevel isolationLevel = IsolationLevel.Unspecified,
            RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
            IEventDispatcher? eventDispatcher = null,
            IScopedNotificationPublisher? scopedNotificationPublisher = null,
            bool? scopeFileSystems = null,
            bool callContext = false,
            bool autoComplete = false) =>
            _inner.CreateCoreScope(isolationLevel, repositoryCacheMode, eventDispatcher, scopedNotificationPublisher, scopeFileSystems, callContext, autoComplete);

        public IScope CreateDetachedScope(
            IsolationLevel isolationLevel = IsolationLevel.Unspecified,
            RepositoryCacheMode repositoryCacheMode = RepositoryCacheMode.Unspecified,
            IEventDispatcher? eventDispatcher = null,
            IScopedNotificationPublisher? scopedNotificationPublisher = null,
            bool? scopeFileSystems = null) =>
            _inner.CreateDetachedScope(isolationLevel, repositoryCacheMode, eventDispatcher, scopedNotificationPublisher, scopeFileSystems);

        public void AttachScope(IScope other, bool callContext = false) => _inner.AttachScope(other, callContext);

        public IScope DetachScope() => _inner.DetachScope();

        public IQuery<T> CreateQuery<T>() => _inner.CreateQuery<T>();
    }
}
