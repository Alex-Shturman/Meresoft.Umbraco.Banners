using System.Data.Common;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// Two editors can pick the same alias at the same moment. The loser passes the "alias is free" check and then
/// hits the unique index; that must come back as DuplicateAlias, not as a server error.
/// </summary>
[TestFixture]
public class ZoneAliasRaceTests : BannersServiceTestBase
{
    private static string? _hideAliasOnce;
    private static bool _addAlwaysFails;

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddSingleton<IZoneRepository>(sp => new RacingZoneRepository(ActivatorUtilities.CreateInstance<ZoneRepository>(sp)));
    }

    [SetUp]
    public void Reset()
    {
        _hideAliasOnce = null;
        _addAlwaysFails = false;
    }

    private static BannerZoneModel Model(string alias) => new() { Alias = alias, Name = "Zone " + alias, ShowCount = 1 };

    [Test]
    public async Task Create_WhenTheAliasIsTakenBetweenCheckAndInsert_ReturnsDuplicateAlias()
    {
        await NewZoneAsync("taken");
        _hideAliasOnce = "taken";

        var attempt = await ZoneService.CreateAsync(Model("taken"), User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.DuplicateAlias));
    }

    [Test]
    public async Task Update_WhenTheAliasIsTakenBetweenCheckAndWrite_ReturnsDuplicateAlias()
    {
        await NewZoneAsync("taken");
        BannerZone other = await NewZoneAsync("other");
        Clock.Advance(TimeSpan.FromMinutes(1));
        _hideAliasOnce = "taken";
        BannerZoneModel change = Model("taken");
        change.UpdateDate = other.UpdateDate;

        var attempt = await ZoneService.UpdateAsync(other.Key, change, User);

        Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.DuplicateAlias));
    }

    [Test]
    public void Create_WhenTheDatabaseFailsAndTheAliasIsFree_TheErrorIsNotHidden()
    {
        _addAlwaysFails = true;

        Assert.That(async () => await ZoneService.CreateAsync(Model("free"), User), Throws.InstanceOf<DbException>());
    }

    private sealed class TestDbException : DbException
    {
        public TestDbException() : base("simulated database failure")
        {
        }
    }

    private sealed class RacingZoneRepository : IZoneRepository
    {
        private readonly IZoneRepository _inner;

        public RacingZoneRepository(IZoneRepository inner) => _inner = inner;

        public BannerZone? GetByAlias(string alias)
        {
            if (_hideAliasOnce == alias)
            {
                _hideAliasOnce = null;
                return null; // The other editor's zone is not visible yet at the moment of the check.
            }

            return _inner.GetByAlias(alias);
        }

        public BannerZone Add(BannerZone zone) => _addAlwaysFails ? throw new TestDbException() : _inner.Add(zone);
        public bool Update(BannerZone zone, DateTime expectedUpdateDate) => _inner.Update(zone, expectedUpdateDate);
        public void Delete(int id) => _inner.Delete(id);
        public BannerZone? GetByKey(Guid key) => _inner.GetByKey(key);
        public BannerZone? GetById(int id) => _inner.GetById(id);
        public IReadOnlyList<BannerZone> GetAll() => _inner.GetAll();
        public IReadOnlyList<Guid> GetAssignedBannerKeys(int zoneId) => _inner.GetAssignedBannerKeys(zoneId);
    }
}
