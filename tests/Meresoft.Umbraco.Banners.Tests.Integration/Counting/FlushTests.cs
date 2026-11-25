using System.Data.Common;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>Writing the counter buffer to the database (research R7, FR-012).</summary>
[TestFixture]
public class FlushTests : BannersServiceTestBase
{
    private static readonly DateOnly Day = new(2027, 3, 1);
    private static readonly HashSet<int> FailOnce = [];
    private static readonly HashSet<int> FailAlways = [];

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IStatisticsRepository>(sp => new FailingRepository(ActivatorUtilities.CreateInstance<StatisticsRepository>(sp)));
    }

    [SetUp]
    public void ResetFailures()
    {
        FailOnce.Clear();
        FailAlways.Clear();
    }

    private CounterBuffer Buffer => GetRequiredService<CounterBuffer>();

    private CounterFlusher NewFlusher(CounterBuffer buffer) => ActivatorUtilities.CreateInstance<CounterFlusher>(Services, buffer);

    // Rows are read and written through the repository and plain statements, never through the table mapping of Day,
    // so nothing here depends on how one database stores a date (the SQL Server job of CI runs these tests too).
    private List<DailyStatRow> Rows()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return [.. GetRequiredService<IStatisticsRepository>().GetDailyRows(new DateOnly(2000, 1, 1), new DateOnly(2100, 1, 1), null, null)];
    }

    private void InsertRow(int bannerId, int zoneId, long impressions, long clicks, bool imported = false)
    {
        using IScope scope = ScopeProvider.CreateScope();
        // Written without the failing decorator of this fixture: the other server's insert.
        ActivatorUtilities.CreateInstance<StatisticsRepository>(Services).AddDelta(new CounterEntry(Day, bannerId, zoneId, impressions, clicks));
        if (imported)
        {
            scope.Database.Execute(
                "UPDATE meresoftBannersDailyStat SET IsImported = @0 WHERE BannerId = @1 AND ZoneId = @2",
                true,
                bannerId,
                zoneId);
        }

        scope.Complete();
    }

    [Test]
    public void AFlush_AddsTheDeltasToExistingRows_AndKeepsTheImportedFlag()
    {
        InsertRow(1, 10, 100, 4, imported: true);
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddClick(Day, 1, 10);

        NewFlusher(Buffer).Flush();

        DailyStatRow row = Rows().Single();
        Assert.That((row.Impressions, row.Clicks, row.IsImported), Is.EqualTo((102L, 5L, true)));
        Assert.That(Buffer.Swap(), Is.Empty, "nothing is left to write");
    }

    [Test]
    public void AFlush_InsertsRowsThatDoNotExistYet()
    {
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddClick(Day, 2, 10);

        NewFlusher(Buffer).Flush();

        Assert.That(Rows().Select(r => (r.BannerId, r.ZoneId, r.Impressions, r.Clicks)), Is.EquivalentTo(new[] { (1, 10, 1L, 0L), (2, 10, 0L, 1L) }));
    }

    [Test]
    public async Task TwoServersWritingTheSameNewRowAtOnce_BothEndUpCounted()
    {
        var other = new CounterBuffer(TimeProvider.System);
        for (int i = 0; i < 40; i++)
        {
            Buffer.AddImpression(Day, 1, 10);
            other.AddImpression(Day, 1, 10);
            other.AddClick(Day, 1, 10);
        }

        CounterFlusher first = NewFlusher(Buffer);
        CounterFlusher second = NewFlusher(other);
        await Task.WhenAll(Task.Run(first.Flush), Task.Run(second.Flush));

        DailyStatRow row = Rows().Single();
        Assert.That((row.Impressions, row.Clicks), Is.EqualTo((80L, 40L)));
    }

    [Test]
    public void ARowInsertedByAnotherServerFirst_IsCountedOnTheRetry_NotLostAndNotDoubled()
    {
        // The other server's insert wins; this server's insert then hits the primary key, which a database reports as an error.
        InsertRow(1, 10, 7, 0);
        FailOnce.Add(1);
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddImpression(Day, 1, 10);

        NewFlusher(Buffer).Flush();

        Assert.That(Rows().Single().Impressions, Is.EqualTo(9));
        Assert.That(Buffer.Swap(), Is.Empty);
    }

    [Test]
    public void AFailedWrite_PutsTheCountsBack_AndTheOtherRowsAreStillWritten()
    {
        FailAlways.Add(1);
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddClick(Day, 1, 10);
        Buffer.AddImpression(Day, 2, 10);

        NewFlusher(Buffer).Flush();

        Assert.That(Rows().Select(r => r.BannerId), Is.EqualTo(new[] { 2 }));
        Assert.That(Buffer.Swap(), Is.EqualTo(new[] { new CounterEntry(Day, 1, 10, 1, 1) }), "kept for the next run");
    }

    [Test]
    public void ApplicationStopping_FlushesTheBuffer()
    {
        Buffer.AddImpression(Day, 3, 10);

        GetRequiredService<IEventAggregator>().Publish(new UmbracoApplicationStoppingNotification(isRestarting: false));

        Assert.That(Rows().Single().Impressions, Is.EqualTo(1));
    }

    [Test]
    public async Task ServingAPageAndCounting_WriteNothingToTheDatabase_OnlyTheFlushDoes()
    {
        await NewZoneAsync("home-top");
        await GetRequiredService<Meresoft.Umbraco.Banners.Caching.ILiveSnapshotAccessor>().RebuildAsync();
        var renderer = GetRequiredService<Meresoft.Umbraco.Banners.Rendering.BannerZoneRenderer>();

        renderer.Render(new Dictionary<object, object?>(), renderer.Select("home-top"), null, null);
        Buffer.AddImpression(Day, 1, 10);
        Buffer.AddClick(Day, 1, 10);

        Assert.That(Rows(), Is.Empty, "FR-012: counts stay in memory until the flush");
    }

    [Test]
    public async Task TheFlushJob_WritesTheCounters()
    {
        Buffer.AddImpression(Day, 1, 10);

        await ActivatorUtilities.CreateInstance<BannersFlushJob>(Services).RunJobAsync(CancellationToken.None);

        Assert.That(Rows().Single().Impressions, Is.EqualTo(1));
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException() : base("simulated primary key violation")
        {
        }
    }

    private sealed class FailingRepository : IStatisticsRepository
    {
        private readonly IStatisticsRepository _inner;

        public FailingRepository(IStatisticsRepository inner) => _inner = inner;

        public void AddDelta(CounterEntry delta)
        {
            if (FailAlways.Contains(delta.BannerId) || FailOnce.Remove(delta.BannerId))
            {
                throw new TestDbException();
            }

            _inner.AddDelta(delta);
        }

        public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId) =>
            _inner.GetDailyRows(from, to, zoneId, bannerId);

        public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds) => _inner.GetTotals(bannerIds);
    }
}
