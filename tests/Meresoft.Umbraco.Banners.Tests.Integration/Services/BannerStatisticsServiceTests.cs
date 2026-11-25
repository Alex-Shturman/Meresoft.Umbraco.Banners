using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

[TestFixture]
public class BannerStatisticsServiceTests : BannersServiceTestBase
{
    private static readonly DateOnly March1 = new(2027, 3, 1);

    private BannerZone _home = null!;
    private BannerZone _side = null!;
    private Banner _summer = null!;
    private Banner _winter = null!;

    private IBannerStatisticsService Statistics => GetRequiredService<IBannerStatisticsService>();

    [SetUp]
    public async Task Prepare()
    {
        _home = await NewZoneAsync("home-top");
        _side = await NewZoneAsync("sidebar");
        _summer = await NewBannerAsync(ImageDraft(NewMediaKey(), _home.Key, _side.Key));
        BannerDraftModel winterDraft = ImageDraft(NewMediaKey(), _home.Key);
        winterDraft.Name = "Winter sale";
        _winter = await NewBannerAsync(winterDraft);
    }

    private void Stat(DateOnly day, int bannerId, int zoneId, long impressions, long clicks, bool imported = false)
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Insert(new DailyStatDto { Day = day, BannerId = bannerId, ZoneId = zoneId, Impressions = impressions, Clicks = clicks, IsImported = imported });
        scope.Complete();
    }

    private async Task<BannerReport> ReportAsync(DateOnly from, DateOnly to, Guid? zone = null, Guid? banner = null)
    {
        Attempt<BannerReport, BannerOperationStatus> attempt = await Statistics.GetReportAsync(new BannerReportQuery { From = from, To = to, ZoneKey = zone, BannerKey = banner });
        Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
        return attempt.Result!;
    }

    [Test]
    public async Task OneHundredImpressionsAndThreeClicks_GiveThreePercent()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);

        BannerReport report = await ReportAsync(March1, March1);

        Assert.That((report.Totals.Impressions, report.Totals.Clicks, report.Totals.Ctr), Is.EqualTo((100L, 3L, (decimal?)3.0m)));
    }

    [Test]
    public async Task TotalsRowsAndTheDailySeries_AreSummedOverTheWholePeriod()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);
        Stat(March1, _summer.Id, _side.Id, 50, 1);
        Stat(March1.AddDays(1), _summer.Id, _home.Id, 10, 0, imported: true);
        Stat(March1.AddDays(1), _winter.Id, _home.Id, 20, 2);
        Stat(March1.AddDays(40), _winter.Id, _home.Id, 999, 99);

        BannerReport report = await ReportAsync(March1, March1.AddDays(3));

        Assert.That((report.Totals.Impressions, report.Totals.Clicks), Is.EqualTo((180L, 6L)));
        Assert.That(report.Rows, Has.Count.EqualTo(3), "one row per banner per zone");
        BannerReportRow summerHome = report.Rows.Single(r => r.BannerKey == _summer.Key && r.ZoneAlias == "home-top");
        Assert.That((summerHome.BannerName, summerHome.Impressions, summerHome.Clicks, summerHome.Ctr), Is.EqualTo(("Summer promo", 110L, 3L, (decimal?)2.7m)));
        Assert.That(report.Daily.Select(d => d.Day), Is.EqualTo(new[] { March1, March1.AddDays(1), March1.AddDays(2), March1.AddDays(3) }), "every day of the period, also the quiet ones");
        Assert.That(report.Daily.Select(d => (d.Impressions, d.Clicks, d.Imported)), Is.EqualTo(new[] { (150L, 4L, false), (30L, 2L, true), (0L, 0L, false), (0L, 0L, false) }));
    }

    [Test]
    public async Task TheZoneFilter_KeepsOnlyThatZone()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);
        Stat(March1, _summer.Id, _side.Id, 50, 1);

        BannerReport report = await ReportAsync(March1, March1, zone: _side.Key);

        Assert.That(report.Totals.Impressions, Is.EqualTo(50));
        Assert.That(report.Rows.Select(r => r.ZoneAlias), Is.EqualTo(new[] { "sidebar" }));
    }

    [Test]
    public async Task TheBannerFilter_KeepsOnlyThatBanner()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);
        Stat(March1, _winter.Id, _home.Id, 20, 2);

        BannerReport report = await ReportAsync(March1, March1, banner: _winter.Key);

        Assert.That(report.Totals.Impressions, Is.EqualTo(20));
        Assert.That(report.Rows.Single().BannerName, Is.EqualTo("Winter sale"));
    }

    [Test]
    public async Task AFilterForAnUnknownKey_GivesAnEmptyReport()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);

        BannerReport report = await ReportAsync(March1, March1, zone: Guid.NewGuid());

        Assert.That(report.Totals.Impressions, Is.EqualTo(0));
        Assert.That(report.Rows, Is.Empty);
    }

    [Test]
    public async Task StatisticsOfADeletedBannerAndADeletedZone_AreKeptWithPlaceholderNames()
    {
        Stat(March1, 98_001, _home.Id, 7, 1);
        Stat(March1, _summer.Id, 98_002, 5, 0);

        BannerReport report = await ReportAsync(March1, March1);

        BannerReportRow deletedBanner = report.Rows.Single(r => r.BannerKey is null);
        Assert.That((deletedBanner.BannerName, deletedBanner.ZoneAlias), Is.EqualTo((ReportNames.DeletedBanner, "home-top")));
        Assert.That(report.Rows.Single(r => r.BannerKey == _summer.Key).ZoneAlias, Is.EqualTo(ReportNames.DeletedZone));
        Assert.That(report.Totals.Impressions, Is.EqualTo(12));
    }

    [Test]
    public async Task ACtrIsNull_WhenARowHasNoImpressions()
    {
        Stat(March1, _summer.Id, _home.Id, 0, 2);

        BannerReport report = await ReportAsync(March1, March1);

        Assert.That(report.Totals.Ctr, Is.Null);
        Assert.That(report.Rows.Single().Ctr, Is.Null);
    }

    [Test]
    public async Task APeriodOf366DaysIsAllowed_367IsRefused_AndSoIsAnEndBeforeTheStart()
    {
        Assert.That((await Statistics.GetReportAsync(new BannerReportQuery { From = March1, To = March1.AddDays(365) })).Success, Is.True);

        Attempt<BannerReport, BannerOperationStatus> tooLong = await Statistics.GetReportAsync(new BannerReportQuery { From = March1, To = March1.AddDays(366) });
        Attempt<BannerReport, BannerOperationStatus> backwards = await Statistics.GetReportAsync(new BannerReportQuery { From = March1.AddDays(1), To = March1 });

        Assert.That(tooLong.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
        Assert.That(backwards.Status, Is.EqualTo(BannerOperationStatus.InvalidData));
    }

    [Test]
    public async Task TheCsv_HasOneRowPerBannerPerZonePerDay_AndTheHeader()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);
        Stat(March1.AddDays(1), _summer.Id, _home.Id, 10, 0);

        Attempt<string, BannerOperationStatus> csv = await Statistics.GetCsvAsync(new BannerReportQuery { From = March1, To = March1.AddDays(1) });

        Assert.That(csv.Success, Is.True);
        Assert.That(csv.Result!.Split("\r\n", StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[]
        {
            "date,zone,banner,impressions,clicks",
            "2027-03-01,home-top,Summer promo,100,3",
            "2027-03-02,home-top,Summer promo,10,0",
        }));
    }

    [Test]
    public async Task TotalsPerBanner_SumAllZonesAndDays_IncludingImportedRows()
    {
        Stat(March1, _summer.Id, _home.Id, 100, 3);
        Stat(March1.AddDays(5), _summer.Id, _side.Id, 50, 1, imported: true);

        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        IReadOnlyDictionary<int, BannerTotals> totals = GetRequiredService<Meresoft.Umbraco.Banners.Persistence.Repositories.IStatisticsRepository>().GetTotals([_summer.Id, _winter.Id]);

        Assert.That(totals[_summer.Id], Is.EqualTo(new BannerTotals(150, 4)));
        Assert.That(totals.ContainsKey(_winter.Id), Is.False);
    }
}
