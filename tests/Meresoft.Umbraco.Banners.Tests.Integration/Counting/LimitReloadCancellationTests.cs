using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>The limit reload at the end of the flush job: after the zone writes, and cancellable.</summary>
[TestFixture]
public class LimitReloadCancellationTests : GatedRebuildTestBase
{
    private BannersFlushJob NewJob() => ActivatorUtilities.CreateInstance<BannersFlushJob>(Services, Gated);

    private List<string> RequestedAliases()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<RequestedZoneDto>().Select(r => r.Alias).ToList();
    }

    [Test]
    public async Task ASlowRebuild_DoesNotDelayTheRequestedZoneWrites()
    {
        GetRequiredService<RequestedZoneRecorder>().Record("asked-by-a-template");
        Task rebuild = await StartBlockedRebuildAsync();

        // The rebuild holds the snapshot lock, so the reload at the end of the job waits; the zone is written before it.
        Task job = NewJob().RunJobAsync(CancellationToken.None);
        await Task.Delay(300);

        Assert.That(job.IsCompleted, Is.False, "the reload waits for the rebuild");
        Assert.That(RequestedAliases(), Does.Contain("asked-by-a-template"));

        ReleaseRebuild();
        await Task.WhenAll(rebuild, job);
    }

    [Test]
    public async Task CancellingTheJob_StopsTheWaitForTheSnapshotLock()
    {
        Task rebuild = await StartBlockedRebuildAsync();
        using var cts = new CancellationTokenSource();

        Task job = NewJob().RunJobAsync(cts.Token);
        await Task.Delay(200);
        cts.Cancel();

        Assert.That(async () => await job, Throws.InstanceOf<OperationCanceledException>());

        ReleaseRebuild();
        await rebuild;
    }
}
