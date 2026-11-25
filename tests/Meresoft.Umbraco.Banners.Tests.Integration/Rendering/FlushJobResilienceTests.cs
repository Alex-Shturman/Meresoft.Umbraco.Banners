using System.Data.Common;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Rendering;

/// <summary>A failure while writing one requested zone must not lose the others (two servers can race on the same new alias).</summary>
[TestFixture]
public class FlushJobResilienceTests : BannersServiceTestBase
{
    private static readonly HashSet<string> FailOnce = [];
    private static readonly HashSet<string> FailAlways = [];

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IRequestedZoneRepository>(sp => new FailingRepository(ActivatorUtilities.CreateInstance<RequestedZoneRepository>(sp)));
    }

    [SetUp]
    public void ResetFailures()
    {
        FailOnce.Clear();
        FailAlways.Clear();
    }

    private async Task<List<string>> FlushAndReadAsync(params string[] aliases)
    {
        RequestedZoneRecorder recorder = GetRequiredService<RequestedZoneRecorder>();
        foreach (string alias in aliases)
        {
            recorder.Record(alias);
        }

        await ActivatorUtilities.CreateInstance<BannersFlushJob>(Services).RunJobAsync(CancellationToken.None);

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<RequestedZoneDto>().Select(r => r.Alias).OrderBy(a => a).ToList();
    }

    [Test]
    public async Task AnAliasThatFailsOnceWithADatabaseError_IsRetriedAndWritten_AndTheOthersAreNotLost()
    {
        FailOnce.Add("racy");

        List<string> stored = await FlushAndReadAsync("first", "racy", "last");

        Assert.That(stored, Is.EqualTo(new[] { "first", "last", "racy" }));
    }

    [Test]
    public async Task AnAliasThatKeepsFailing_DoesNotStopTheOthers()
    {
        FailAlways.Add("broken");

        List<string> stored = await FlushAndReadAsync("first", "broken", "last");

        Assert.That(stored, Is.EqualTo(new[] { "first", "last" }));
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException() : base("simulated unique index violation")
        {
        }
    }

    private sealed class FailingRepository : IRequestedZoneRepository
    {
        private readonly IRequestedZoneRepository _inner;

        public FailingRepository(IRequestedZoneRepository inner) => _inner = inner;

        public void Upsert(string alias, DateTime firstSeenUtc, DateTime lastSeenUtc)
        {
            if (FailAlways.Contains(alias) || FailOnce.Remove(alias))
            {
                throw new TestDbException();
            }

            _inner.Upsert(alias, firstSeenUtc, lastSeenUtc);
        }

        public bool Delete(string alias) => _inner.Delete(alias);

        public IReadOnlyList<RequestedZone> GetAll() => _inner.GetAll();
    }
}
