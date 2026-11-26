using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>
/// A snapshot accessor whose rebuild can be held inside <c>BuildAsync</c> (at the media lookup), so a test can
/// start other work while the rebuild owns the snapshot lock.
/// </summary>
public abstract class GatedRebuildTestBase : BannersServiceTestBase
{
    protected static readonly DateOnly Day = new(2027, 3, 1);

    // One fixture instance serves all its tests, so both are made new for each test.
    private TaskCompletionSource _entered = null!;
    private TaskCompletionSource _gate = null!;

    protected BannerZone Zone { get; private set; } = null!;

    protected Banner Limited { get; private set; } = null!;

    protected Banner Unlimited { get; private set; } = null!;

    protected ILiveSnapshotAccessor Gated { get; private set; } = null!;

    [SetUp]
    public async Task PrepareGated()
    {
        _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Zone = await NewZoneAsync("gated-zone");
        Limited = await PublishAsync(limit: 3);
        Unlimited = await PublishAsync(limit: null);

        IPublishedMediaCache real = GetRequiredService<IPublishedMediaCache>();
        IPublishedMediaCache gated = Substitute.For<IPublishedMediaCache>();
        gated.GetByIdAsync(Arg.Any<Guid>()).Returns(async call =>
        {
            _entered.TrySetResult();
            await _gate.Task;
            return await real.GetByIdAsync(call.Arg<Guid>());
        });
        var builder = new LiveSnapshotBuilder(gated, GetRequiredService<MediaUrlProviderCollection>(), GetRequiredService<IImageUrlGenerator>());
        Gated = ActivatorUtilities.CreateInstance<LiveSnapshotAccessor>(Services, builder);
    }

    private async Task<Banner> PublishAsync(long? limit)
    {
        BannerDraftModel draft = ImageDraft(NewMediaKey(), Zone.Key);
        draft.ImpressionLimit = limit;
        Banner banner = await NewBannerAsync(draft);
        Assert.That((await BannerService.PublishAsync(banner.Key, User)).Success, Is.True);
        return banner;
    }

    /// <summary>Starts a rebuild and returns once it is inside <c>BuildAsync</c>, holding the snapshot lock.</summary>
    protected async Task<Task> StartBlockedRebuildAsync()
    {
        Task rebuild = Gated.RebuildAsync();
        await _entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return rebuild;
    }

    protected void ReleaseRebuild() => _gate.TrySetResult();

    /// <summary>Adds imported statistics for the limited banner, as another server's flush or an import would.</summary>
    protected void AddImportedImpressions(long impressions)
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Insert(new DailyStatDto { Day = Day, BannerId = Limited.Id, ZoneId = Zone.Id, Impressions = impressions, Clicks = 0, IsImported = true });
        scope.Complete();
    }

    [TearDown]
    public void OpenTheGate() => ReleaseRebuild();
}
