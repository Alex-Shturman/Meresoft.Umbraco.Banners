using System.Net;
using System.Text.Json;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.ManagementApi;

[TestFixture]
public class ReportsApiTests : BannersApiTestBase
{
    private Guid _zoneKey;
    private Guid _bannerKey;
    private int _zoneId;
    private int _bannerId;

    [SetUp]
    public async Task Prepare()
    {
        (_zoneKey, _) = await CreateZoneAsync("home-top");
        _bannerKey = await CreateBannerAsync(NewMediaKey(), _zoneKey);
        _zoneId = (await GetRequiredService<IBannerZoneService>().GetAsync(_zoneKey))!.Id;
        _bannerId = (await GetRequiredService<IBannerService>().GetAsync(_bannerKey))!.Id;
    }

    [TearDown]
    public void RemoveStats()
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Execute($"DELETE FROM {DailyStatDto.TableName}");
        scope.Complete();
    }

    private void Stat(string day, long impressions, long clicks, bool imported = false)
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Insert(new DailyStatDto { Day = DateOnly.Parse(day), BannerId = _bannerId, ZoneId = _zoneId, Impressions = impressions, Clicks = clicks, IsImported = imported });
        scope.Complete();
    }

    [Test]
    public async Task Report_ReturnsTotalsRowsAndDailySeries()
    {
        Stat("2027-03-01", 100, 3);
        Stat("2027-03-02", 40, 1, imported: true);

        HttpResponseMessage response = await GetAsync("/reports?from=2027-03-01&to=2027-03-03");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        JsonElement report = await ReadAsync(response);
        Assert.Multiple(() =>
        {
            Assert.That(report.GetProperty("totals").GetProperty("impressions").GetInt64(), Is.EqualTo(140));
            Assert.That(report.GetProperty("totals").GetProperty("clicks").GetInt64(), Is.EqualTo(4));
            Assert.That(report.GetProperty("totals").GetProperty("ctr").GetDecimal(), Is.EqualTo(2.9m));
            JsonElement row = report.GetProperty("rows").EnumerateArray().Single();
            Assert.That(row.GetProperty("bannerKey").GetGuid(), Is.EqualTo(_bannerKey));
            Assert.That(row.GetProperty("bannerName").GetString(), Is.EqualTo("Summer promo"));
            Assert.That(row.GetProperty("zoneAlias").GetString(), Is.EqualTo("home-top"));
            JsonElement[] daily = report.GetProperty("daily").EnumerateArray().ToArray();
            Assert.That(daily.Select(d => d.GetProperty("day").GetString()), Is.EqualTo(new[] { "2027-03-01", "2027-03-02", "2027-03-03" }));
            Assert.That(daily.Select(d => d.GetProperty("imported").GetBoolean()), Is.EqualTo(new[] { false, true, false }));
        });
    }

    [Test]
    public async Task OneHundredImpressionsAndThreeClicks_GiveThreePointZeroPercent()
    {
        Stat("2027-03-01", 100, 3);

        JsonElement report = await ReadAsync(await GetAsync("/reports?from=2027-03-01&to=2027-03-01"));

        Assert.That(report.GetProperty("totals").GetProperty("ctr").GetDecimal(), Is.EqualTo(3.0m));
    }

    [Test]
    public async Task TheZoneAndBannerFilters_AreApplied()
    {
        Stat("2027-03-01", 100, 3);

        JsonElement other = await ReadAsync(await GetAsync($"/reports?from=2027-03-01&to=2027-03-01&zone={Guid.NewGuid()}"));
        JsonElement matching = await ReadAsync(await GetAsync($"/reports?from=2027-03-01&to=2027-03-01&zone={_zoneKey}&banner={_bannerKey}"));

        Assert.That(other.GetProperty("totals").GetProperty("impressions").GetInt64(), Is.EqualTo(0));
        Assert.That(matching.GetProperty("totals").GetProperty("impressions").GetInt64(), Is.EqualTo(100));
    }

    [Test]
    public async Task AFullYearIsAllowed_MoreThan366DaysGives400()
    {
        HttpResponseMessage ok = await GetAsync("/reports?from=2027-01-01&to=2027-12-31");
        HttpResponseMessage tooLong = await GetAsync("/reports?from=2027-01-01&to=2028-01-02");

        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(tooLong.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ReadAsync(tooLong)).GetProperty("type").GetString(), Is.EqualTo("invalid-data"));
    }

    [TestCase("")]
    [TestCase("?from=2027-03-01")]
    [TestCase("?to=2027-03-01")]
    [TestCase("?from=nonsense&to=2027-03-01")]
    [TestCase("?from=2027-03-05&to=2027-03-01")]
    public async Task MissingOrInvalidDates_Give400(string query)
    {
        HttpResponseMessage response = await GetAsync("/reports" + query);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Csv_HasTheTextCsvContentType_AFileName_AndTheRows()
    {
        Stat("2027-03-01", 100, 3);

        HttpResponseMessage response = await GetAsync("/reports/csv?from=2027-03-01&to=2027-03-31");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.ToString(), Is.EqualTo("text/csv; charset=utf-8"));
        Assert.That(response.Content.Headers.ContentDisposition?.FileName?.Trim('"'), Is.EqualTo("banners-2027-03-01-2027-03-31.csv"));
        Assert.That((await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[]
        {
            "date,zone,banner,impressions,clicks",
            "2027-03-01,home-top,Summer promo,100,3",
        }));
    }

    [Test]
    public async Task Csv_WithTooLongAPeriod_Gives400()
    {
        HttpResponseMessage response = await GetAsync("/reports/csv?from=2026-01-01&to=2027-12-31");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task WithoutSignIn_ReportsAre401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        HttpResponseMessage response = await GetAsync("/reports?from=2027-03-01&to=2027-03-01");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
