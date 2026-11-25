using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

[TestFixture]
public class ZonesApiTests : BannersApiTestBase
{
    [Test]
    public async Task Create_Returns201WithLocation_AndGetReturnsTheZone()
    {
        HttpResponseMessage response = await PostAsync("/zones", ZoneBody("Search-Top", renderMode: "client", showCount: 3));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Guid key = KeyFromLocation(response);
        JsonElement zone = await ReadAsync(await GetAsync("/zones/" + key));
        Assert.Multiple(() =>
        {
            Assert.That(zone.GetProperty("key").GetGuid(), Is.EqualTo(key));
            Assert.That(zone.GetProperty("alias").GetString(), Is.EqualTo("search-top"));
            Assert.That(zone.GetProperty("name").GetString(), Is.EqualTo("Zone Search-Top"));
            Assert.That(zone.GetProperty("showCount").GetInt32(), Is.EqualTo(3));
            Assert.That(zone.GetProperty("imageWidth").GetInt32(), Is.EqualTo(728));
            Assert.That(zone.GetProperty("renderMode").GetString(), Is.EqualTo("client"));
            Assert.That(zone.GetProperty("updateDate").GetDateTime(), Is.Not.EqualTo(default(DateTime)));
        });
    }

    [Test]
    public async Task Create_InvalidData_Returns400WithFieldErrors()
    {
        HttpResponseMessage response = await PostAsync("/zones", ZoneBody("9 not valid"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        JsonElement problem = await ReadAsync(response);
        Assert.That(problem.GetProperty("errors").TryGetProperty("alias", out _), Is.True);
    }

    [Test]
    public async Task Create_DuplicateAlias_Returns409()
    {
        await CreateZoneAsync("dup");

        HttpResponseMessage response = await PostAsync("/zones", ZoneBody("dup"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await ReadAsync(response)).GetProperty("type").GetString(), Is.EqualTo("alias-taken"));
    }

    [Test]
    public async Task Create_RemovesTheMatchingRequestedZone()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new RequestedZoneDto { Alias = "wanted", FirstSeen = DateTime.UtcNow, LastSeen = DateTime.UtcNow });
            scope.Complete();
        }

        await CreateZoneAsync("wanted");

        JsonElement list = await ReadAsync(await GetAsync("/requested-zones"));
        Assert.That(list.EnumerateArray().Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task List_ReturnsZonesWithTheirFields()
    {
        await CreateZoneAsync("alpha");
        await CreateZoneAsync("beta");

        HttpResponseMessage response = await GetAsync("/zones");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonElement list = await ReadAsync(response);
        Assert.That(list.EnumerateArray().Select(z => z.GetProperty("alias").GetString()), Is.EquivalentTo(new[] { "alpha", "beta" }));
        Assert.That(list[0].TryGetProperty("activeBannerCount", out _), Is.True);
    }

    [Test]
    public async Task Get_Unknown_Returns404()
    {
        Assert.That((await GetAsync("/zones/" + Guid.NewGuid())).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Put_UpdatesTheZone()
    {
        (Guid key, JsonElement zone) = await CreateZoneAsync("before");

        HttpResponseMessage response = await PutAsync("/zones/" + key, ZoneBody("after", zone.GetProperty("updateDate").GetDateTime(), 5));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonElement updated = await ReadAsync(await GetAsync("/zones/" + key));
        Assert.That(updated.GetProperty("alias").GetString(), Is.EqualTo("after"));
        Assert.That(updated.GetProperty("showCount").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public async Task Put_InvalidData_Returns400()
    {
        (Guid key, JsonElement zone) = await CreateZoneAsync("valid");

        HttpResponseMessage response = await PutAsync("/zones/" + key, ZoneBody("valid", zone.GetProperty("updateDate").GetDateTime(), 99));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_Unknown_Returns404()
    {
        HttpResponseMessage response = await PutAsync("/zones/" + Guid.NewGuid(), ZoneBody("nope"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Put_StaleUpdateDate_Returns409ChangedByAnotherUser()
    {
        (Guid key, JsonElement zone) = await CreateZoneAsync("race");
        DateTime loaded = zone.GetProperty("updateDate").GetDateTime();
        await Task.Delay(50);
        await PutAsync("/zones/" + key, ZoneBody("race", loaded, 2));

        HttpResponseMessage response = await PutAsync("/zones/" + key, ZoneBody("race", loaded, 3));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await ReadAsync(response)).GetProperty("type").GetString(), Is.EqualTo("changed-by-another-user"));
    }

    [Test]
    public async Task Delete_Returns204_ThenGetIs404()
    {
        (Guid key, _) = await CreateZoneAsync("temp");

        Assert.That((await DeleteAsync("/zones/" + key)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await GetAsync("/zones/" + key)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Unknown_Returns404()
    {
        Assert.That((await DeleteAsync("/zones/" + Guid.NewGuid())).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WhileBannersAreAssigned_Returns409WithTheBannerKeys()
    {
        (Guid zoneKey, _) = await CreateZoneAsync("busy");
        Guid bannerKey = await CreateBannerAsync(NewMediaKey(), zoneKey);

        HttpResponseMessage response = await DeleteAsync("/zones/" + zoneKey);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        JsonElement problem = await ReadAsync(response);
        Assert.That(problem.GetProperty("type").GetString(), Is.EqualTo("zone-in-use"));
        Assert.That(problem.GetProperty("bannerKeys").EnumerateArray().Select(k => k.GetGuid()), Is.EqualTo(new[] { bannerKey }));
    }

    [Test]
    public async Task RequestedZones_AreListed_AndCanBeDismissed()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new RequestedZoneDto { Alias = "seen-one", FirstSeen = new DateTime(2027, 1, 1), LastSeen = new DateTime(2027, 1, 2) });
            scope.Database.Insert(new RequestedZoneDto { Alias = "seen-two", FirstSeen = new DateTime(2027, 1, 1), LastSeen = new DateTime(2027, 1, 3) });
            scope.Complete();
        }

        JsonElement list = await ReadAsync(await GetAsync("/requested-zones"));
        Assert.That(list.EnumerateArray().Select(r => r.GetProperty("alias").GetString()), Is.EquivalentTo(new[] { "seen-one", "seen-two" }));
        Assert.That(list[0].TryGetProperty("firstSeen", out _), Is.True);
        Assert.That(list[0].TryGetProperty("lastSeen", out _), Is.True);

        Assert.That((await DeleteAsync("/requested-zones/seen-one")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        JsonElement after = await ReadAsync(await GetAsync("/requested-zones"));
        Assert.That(after.EnumerateArray().Select(r => r.GetProperty("alias").GetString()), Is.EqualTo(new[] { "seen-two" }));
    }
}
