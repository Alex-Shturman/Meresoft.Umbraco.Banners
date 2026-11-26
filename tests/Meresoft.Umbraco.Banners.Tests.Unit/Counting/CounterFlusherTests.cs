using System.Data.Common;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Counting;

/// <summary>Review 10: flushes never overlap, so the flush at shutdown waits for a running flush and for what it puts back.</summary>
[TestFixture]
public class CounterFlusherTests
{
    private static readonly DateOnly Day = new(2027, 3, 1);

    private CounterBuffer _buffer = null!;
    private BlockingRepository _repository = null!;
    private CounterFlusher _flusher = null!;

    [SetUp]
    public void SetUp()
    {
        _buffer = new CounterBuffer(new FakeTimeProvider(new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero)));
        _repository = new BlockingRepository();
        _flusher = new CounterFlusher(_buffer, Substitute.For<IScopeProvider>(), _repository, NullLogger<CounterFlusher>.Instance);
    }

    [Test]
    public void TheFlushAtShutdown_WaitsForARunningFlush_AndWritesWhatThatFlushPutBack()
    {
        _buffer.AddImpression(Day, 1, 10);
        _repository.FailFirstWriteAfterRelease = true;
        Task running = Task.Run(_flusher.Flush);
        Assert.That(_repository.FirstWriteStarted.Wait(TimeSpan.FromSeconds(5)), Is.True, "the first flush is writing");

        Task stopping = Task.Run(() => new FlushOnStoppingHandler(_flusher).Handle(new UmbracoApplicationStoppingNotification(isRestarting: false)));

        Assert.That(stopping.Wait(TimeSpan.FromMilliseconds(400)), Is.False, "the shutdown flush waits for the running one");
        _repository.Release.Set();
        Assert.That(Task.WaitAll([running, stopping], TimeSpan.FromSeconds(5)), Is.True);

        Assert.That(_repository.Written, Is.EqualTo(new[] { new CounterEntry(Day, 1, 10, 1, 0) }), "written once, by the shutdown flush, after the restore");
        Assert.That(_buffer.Swap(), Is.Empty);
    }

    [Test]
    public void TwoFlushesAtOnce_NeverWriteAtTheSameTime()
    {
        for (int banner = 1; banner <= 30; banner++)
        {
            _buffer.AddImpression(Day, banner, 10);
        }

        _repository.Release.Set();
        _repository.TrackConcurrency = true;
        Task.WaitAll(Task.Run(_flusher.Flush), Task.Run(_flusher.Flush), Task.Run(_flusher.Flush));

        Assert.That(_repository.MaxConcurrentWrites, Is.EqualTo(1));
        Assert.That(_repository.Written, Has.Count.EqualTo(30));
    }

    private sealed class BlockingRepository : IStatisticsRepository
    {
        private int _calls;
        private int _inside;

        public ManualResetEventSlim FirstWriteStarted { get; } = new(false);

        public ManualResetEventSlim Release { get; } = new(false);

        public bool FailFirstWriteAfterRelease { get; set; }

        public bool TrackConcurrency { get; set; }

        public int MaxConcurrentWrites { get; private set; }

        public List<CounterEntry> Written { get; } = [];

        public void AddDelta(CounterEntry delta)
        {
            int inside = Interlocked.Increment(ref _inside);
            try
            {
                lock (Written)
                {
                    MaxConcurrentWrites = Math.Max(MaxConcurrentWrites, inside);
                }

                if (Interlocked.Increment(ref _calls) == 1)
                {
                    FirstWriteStarted.Set();
                    Release.Wait(TimeSpan.FromSeconds(5));
                    if (FailFirstWriteAfterRelease)
                    {
                        throw new SimulatedDbException();
                    }
                }

                if (TrackConcurrency)
                {
                    Thread.Sleep(2);
                }

                lock (Written)
                {
                    Written.Add(delta);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _inside);
            }
        }

        public bool Import(DateOnly day, int bannerId, int zoneId, long impressions, long clicks, bool replace) => throw new NotSupportedException();

        public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId) => [];

        public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds) => new Dictionary<int, BannerTotals>();
    }

    private sealed class SimulatedDbException : DbException
    {
        public SimulatedDbException() : base("simulated write failure")
        {
        }
    }
}
