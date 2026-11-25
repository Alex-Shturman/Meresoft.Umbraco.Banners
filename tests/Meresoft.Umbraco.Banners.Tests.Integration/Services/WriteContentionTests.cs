using System.Diagnostics;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// Real simultaneity on SQLite: the flush job (requested zones and counters) runs while an editor saves, publishes and
/// unpublishes. Every writing scope of the package starts as a deferred SQLite transaction that reads before it writes, and
/// two such scopes can wait for each other (a read lock held while the write lock is asked for). Without the package taking
/// the Umbraco write lock first, a scope can stall until the command timeout of 30 seconds, or for ever. The test fails when
/// any single operation takes more than <see cref="StallLimit"/> or the whole run does not end.
/// </summary>
[TestFixture]
public class WriteContentionTests : BannersServiceTestBase
{
    private static readonly TimeSpan StallLimit = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RunTime = TimeSpan.FromSeconds(6);

    private readonly List<(string Operation, TimeSpan Took)> _operations = [];

    private T Timed<T>(string operation, Func<T> work)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            return work();
        }
        finally
        {
            lock (_operations)
            {
                _operations.Add((operation, watch.Elapsed));
            }
        }
    }

    [Test]
    public async Task TheFlushJobAndAnEditorWritingAtOnce_NeverStallEachOther()
    {
        BannerZone zone = await NewZoneAsync("home-top");
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        var recorder = GetRequiredService<RequestedZoneRecorder>();
        var buffer = GetRequiredService<CounterBuffer>();
        BannersFlushJob job = ActivatorUtilities.CreateInstance<BannersFlushJob>(Services);
        using var stop = new CancellationTokenSource(RunTime);

        Task flushing = Task.Run(async () =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                recorder.Record($"requested-{i % 40}");
                buffer.AddImpression(new DateOnly(2027, 3, 1), banner.Id, zone.Id);
                await Timed("flush job", () => job.RunJobAsync(CancellationToken.None));
                await Task.Delay(5);
            }
        });

        Task editing = Task.Run(async () =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                BannerDraftModel draft = ImageDraft(banner.Draft!.MediaKey!.Value, zone.Key);
                draft.AltText = "Invented text " + i;
                await Timed("save", () => BannerService.SaveDraftAsync(banner.Key, draft, User));
                await Timed("publish", () => BannerService.PublishAsync(banner.Key, User));
                await Timed("unpublish", () => BannerService.UnpublishAsync(banner.Key, User));

                // Copy and delete write too: a banner with its versions and assignments, then its removal.
                var copy = await Timed("copy", () => BannerService.CopyAsync(banner.Key, "Copy " + i, User));
                if (copy.Success)
                {
                    await Timed("delete banner", () => BannerService.DeleteAsync(copy.Result!.Key, User));
                }
            }
        });

        Task zoneUpdates = Task.Run(async () =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                // No update date: the edit is not guarded, so every round writes.
                await Timed("update zone", () => ZoneService.UpdateAsync(zone.Key, new BannerZoneModel { Alias = "home-top", Name = "Home " + i, ShowCount = 1 }, User));
            }
        });

        Task zones = Task.Run(async () =>
        {
            for (int i = 0; !stop.IsCancellationRequested; i++)
            {
                var attempt = await Timed("create zone", () => ZoneService.CreateAsync(new BannerZoneModel { Alias = $"extra-{i}", Name = "Extra " + i, ShowCount = 1 }, User));
                if (attempt.Success)
                {
                    await Timed("delete zone", () => ZoneService.DeleteAsync(attempt.Result!.Key, User));
                }
            }
        });

        Task all = Task.WhenAll(flushing, editing, zones, zoneUpdates);
        await all.WaitAsync(RunTime + TimeSpan.FromSeconds(40));

        (string Operation, TimeSpan Took) slowest = _operations.MaxBy(o => o.Took);
        Assert.That(_operations, Has.Count.GreaterThan(30), "the loops really ran");
        foreach (string operation in new[] { "flush job", "save", "publish", "unpublish", "copy", "delete banner", "update zone", "create zone", "delete zone" })
        {
            Assert.That(_operations.Any(o => o.Operation == operation), Is.True, $"'{operation}' ran at least once");
        }

        Assert.That(slowest.Took, Is.LessThan(StallLimit), $"the slowest operation was '{slowest.Operation}'");
    }

    [Test]
    public async Task TwoFlushesOfTheSameNewRequestedZone_EndWithOneRow_NoMatterWhoWritesFirst()
    {
        var recorder = GetRequiredService<RequestedZoneRecorder>();
        BannersFlushJob first = ActivatorUtilities.CreateInstance<BannersFlushJob>(Services);
        BannersFlushJob second = ActivatorUtilities.CreateInstance<BannersFlushJob>(Services);

        for (int round = 0; round < 25; round++)
        {
            string alias = "race-" + round;
            // Two "servers" saw the alias in the same minute; each flush writes its own sighting.
            recorder.Record(alias);
            await first.RunJobAsync(CancellationToken.None);
            Clock.Advance(TimeSpan.FromSeconds(30));
            recorder.Record(alias);
            await Task.WhenAll(Task.Run(() => second.RunJobAsync(CancellationToken.None)), Task.Run(() => first.RunJobAsync(CancellationToken.None)));
        }

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        List<RequestedZoneDto> rows = scope.Database.Fetch<RequestedZoneDto>();
        Assert.That(rows.Select(r => r.Alias).Distinct().Count(), Is.EqualTo(25));
        Assert.That(rows, Has.Count.EqualTo(25), "one row per alias, never a duplicate or a lost row");
        Assert.That(rows.All(r => r.LastSeen >= r.FirstSeen), Is.True, "the last seen time never moves back");
    }
}
