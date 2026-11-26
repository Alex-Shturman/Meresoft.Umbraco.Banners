using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

/// <summary>Rebuild requests that arrive while a rebuild runs collapse into one more rebuild.</summary>
[TestFixture]
public class LiveSnapshotAccessorCoalescingTests
{
    private readonly ManualResetEventSlim _firstReadStarted = new(false);
    private readonly ManualResetEventSlim _releaseFirstRead = new(false);
    private string _currentAlias = "v0";
    private int _builds;
    private LiveSnapshotAccessor _accessor = null!;

    [SetUp]
    public void SetUp()
    {
        _firstReadStarted.Reset();
        _releaseFirstRead.Reset();
        _currentAlias = "v0";
        _builds = 0;

        var zones = new FakeZoneRepository(() =>
        {
            // What the build reads is fixed at this moment; the first build then stays inside the read.
            string alias = _currentAlias;
            if (Interlocked.Increment(ref _builds) == 1)
            {
                _firstReadStarted.Set();
                _releaseFirstRead.Wait(TimeSpan.FromSeconds(20));
            }

            return [new BannerZone { Id = 1, Key = Guid.NewGuid(), Alias = alias, Name = alias }];
        });
        var banners = new FakeBannerRepository();
        var statistics = new FakeStatisticsRepository();
        IScopeProvider scopes = Substitute.For<IScopeProvider>();
        var builder = new LiveSnapshotBuilder(
            Substitute.For<IPublishedMediaCache>(),
            new MediaUrlProviderCollection(() => []),
            Substitute.For<IImageUrlGenerator>());
        _accessor = new LiveSnapshotAccessor(scopes, zones, banners, statistics, builder, NullLogger<LiveSnapshotAccessor>.Instance);
    }

    [Test]
    public async Task ManyRequestsDuringARunningRebuild_EndInAtMostTwoBuilds_AndTheLastChangeIsInTheSnapshot()
    {
        Task first = Task.Run(() => _accessor.RebuildAsync());
        Assert.That(_firstReadStarted.Wait(TimeSpan.FromSeconds(10)), Is.True, "the first rebuild is running");

        var others = new List<Task>();
        for (int i = 1; i <= 50; i++)
        {
            _currentAlias = "v" + i;
            others.Add(_accessor.RebuildAsync());
        }

        _releaseFirstRead.Set();
        await Task.WhenAll(others.Append(first)).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.That(_builds, Is.LessThanOrEqualTo(2), "50 requests collapse into one more build");
        Assert.That(_accessor.Current.Zones.Keys, Is.EqualTo(new[] { "v50" }), "the last change is in the final snapshot");
    }

    [Test]
    public async Task ARequestAfterTheRunIsOver_StartsANewBuild()
    {
        _releaseFirstRead.Set();
        await _accessor.RebuildAsync();
        _currentAlias = "later";

        await _accessor.RebuildAsync();

        Assert.That(_builds, Is.EqualTo(2));
        Assert.That(_accessor.Current.Zones.Keys, Is.EqualTo(new[] { "later" }));
    }

    [Test]
    public async Task AFailedBuild_FailsTheCallersWaitingForIt_AndTheNextRequestStillBuilds()
    {
        var zones = new FakeZoneRepository(() => throw new InvalidOperationException("database down"));
        var accessor = new LiveSnapshotAccessor(
            Substitute.For<IScopeProvider>(),
            zones,
            new FakeBannerRepository(),
            new FakeStatisticsRepository(),
            new LiveSnapshotBuilder(Substitute.For<IPublishedMediaCache>(), new MediaUrlProviderCollection(() => []), Substitute.For<IImageUrlGenerator>()),
            NullLogger<LiveSnapshotAccessor>.Instance);

        Assert.That(async () => await accessor.RebuildAsync(), Throws.InvalidOperationException);
        Assert.That(async () => await accessor.RebuildAsync(), Throws.InvalidOperationException, "the running flag was reset");
        await Task.CompletedTask;
    }

    // Hand written fakes: the repository interfaces are internal and the package does not open them to the proxy generator.
    private sealed class FakeZoneRepository(Func<IReadOnlyList<BannerZone>> getAll) : IZoneRepository
    {
        public IReadOnlyList<BannerZone> GetAll() => getAll();

        public BannerZone Add(BannerZone zone) => throw new NotSupportedException();

        public bool Update(BannerZone zone, DateTime expectedUpdateDate) => throw new NotSupportedException();

        public void Delete(int id) => throw new NotSupportedException();

        public BannerZone? GetByKey(Guid key) => throw new NotSupportedException();

        public BannerZone? GetByAlias(string alias) => throw new NotSupportedException();

        public BannerZone? GetById(int id) => throw new NotSupportedException();

        public IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId) => throw new NotSupportedException();
    }

    private sealed class FakeBannerRepository : IBannerRepository
    {
        public IReadOnlyList<Banner> GetAllPublished() => [];

        public Banner Add(Banner banner) => throw new NotSupportedException();

        public Banner? GetByKey(Guid key) => throw new NotSupportedException();

        public Banner? GetById(int id) => throw new NotSupportedException();

        public bool SaveDraft(Banner banner, DateTime expectedUpdateDate) => throw new NotSupportedException();

        public bool CopyDraftOverLive(int bannerId, DateTime publishDateUtc, DateTime expectedUpdateDate) => throw new NotSupportedException();

        public void SetPublished(int bannerId, bool isPublished) => throw new NotSupportedException();

        public void Delete(int id) => throw new NotSupportedException();

        public IReadOnlyDictionary<int, (Guid Key, string Name)> GetNames(IReadOnlyCollection<int> ids) => throw new NotSupportedException();

        public int GetMaxId() => throw new NotSupportedException();

        public (IReadOnlyList<Banner> Items, long Total) GetPaged(BannerFilter filter) => throw new NotSupportedException();
    }

    private sealed class FakeStatisticsRepository : IStatisticsRepository
    {
        public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds) => new Dictionary<int, BannerTotals>();

        public void AddDelta(CounterEntry delta) => throw new NotSupportedException();

        public bool Import(DateOnly day, int bannerId, int zoneId, long impressions, long clicks, bool replace) => throw new NotSupportedException();

        public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId) => throw new NotSupportedException();
    }
}
