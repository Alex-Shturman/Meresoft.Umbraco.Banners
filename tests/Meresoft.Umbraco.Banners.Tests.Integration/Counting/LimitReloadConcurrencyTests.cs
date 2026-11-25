using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Counting;

/// <summary>A limit reload and a rebuild never overlap: the reload waits, and its result is never put under a newer rebuild.</summary>
[TestFixture]
public class LimitReloadConcurrencyTests : GatedRebuildTestBase
{
    private static IReadOnlyList<int> Served(LiveSnapshot snapshot) =>
        [.. snapshot.Zones["gated-zone"].Candidates.Select(c => c.BannerId).Order()];

    [Test]
    public async Task AReloadStartedDuringARebuild_WaitsForIt_AndThenAppliesTheLimitsToTheRebuildsContent()
    {
        // The rebuild has read its totals (none yet) and is held before it builds the snapshot.
        Task rebuild = await StartBlockedRebuildAsync();

        // Meanwhile the limited banner reaches its limit, as another server's flush would write it.
        AddImportedImpressions(3);
        Task reload = Gated.ReloadLimitsAsync(CancellationToken.None);
        await Task.Delay(300);
        Assert.That(reload.IsCompleted, Is.False, "the reload does not finish while the rebuild runs");
        Assert.That(Gated.IsBuilt, Is.False);

        ReleaseRebuild();
        await Task.WhenAll(rebuild, reload);

        // The rebuild's content (both banners, built from totals read before the import) with the limit then applied.
        Assert.That(Served(Gated.Current), Is.EqualTo(new[] { Unlimited.Id }));
        Assert.That(Gated.IsBuilt, Is.True);
    }

    [Test]
    public async Task AReloadBeforeTheFirstBuild_IsANoOp()
    {
        await Gated.ReloadLimitsAsync(CancellationToken.None);

        Assert.That(Gated.IsBuilt, Is.False);
        Assert.That(Gated.Current, Is.SameAs(LiveSnapshot.Empty));
    }
}
