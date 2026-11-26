using System.Diagnostics;
using Meresoft.Umbraco.Banners.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

[TestFixture]
public class LiveSnapshotAccessorWaitTests
{
    // The wait never touches the database, so the build collaborators are not needed.
    private static LiveSnapshotAccessor Create() =>
        new(null!, null!, null!, null!, null!, NullLogger<LiveSnapshotAccessor>.Instance);

    [Test]
    public async Task WhenNeverBuilt_TheWaitEndsAtTheBound_AndReturnsFalse()
    {
        LiveSnapshotAccessor accessor = Create();
        var watch = Stopwatch.StartNew();

        bool built = await accessor.WaitForFirstBuildAsync(TimeSpan.FromMilliseconds(150));

        Assert.That(built, Is.False);
        Assert.That(watch.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(100)).And.LessThan(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task WhenTheStartupBuildGivesUp_TheWaitEndsAtOnce_AndReturnsFalse()
    {
        LiveSnapshotAccessor accessor = Create();
        Task<bool> wait = accessor.WaitForFirstBuildAsync(TimeSpan.FromSeconds(30));

        accessor.MarkStartupBuildFinished();

        Assert.That(await wait.WaitAsync(TimeSpan.FromSeconds(5)), Is.False);
    }

    [Test]
    public async Task AfterTheStartupBuildGaveUp_NewWaitsDoNotWait()
    {
        LiveSnapshotAccessor accessor = Create();
        accessor.MarkStartupBuildFinished();
        var watch = Stopwatch.StartNew();

        bool built = await accessor.WaitForFirstBuildAsync(TimeSpan.FromSeconds(30));

        Assert.That(built, Is.False);
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task ACancelledWait_IsCancelled()
    {
        LiveSnapshotAccessor accessor = Create();
        using var cts = new CancellationTokenSource(50);

        Assert.That(async () => await accessor.WaitForFirstBuildAsync(TimeSpan.FromSeconds(30), cts.Token), Throws.InstanceOf<OperationCanceledException>());
        await Task.CompletedTask;
    }
}
