using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>FR-016: the import surface for statistics history.</summary>
[TestFixture]
public class BannerImportServiceTests : BannersServiceTestBase
{
    private static readonly DateOnly March1 = new(2027, 3, 1);

    private BannerZone _home = null!;
    private BannerZone _side = null!;
    private Banner _summer = null!;

    private IBannerImportService Import => GetRequiredService<IBannerImportService>();

    private ILiveSnapshotAccessor Snapshot => GetRequiredService<ILiveSnapshotAccessor>();

    [SetUp]
    public async Task Prepare()
    {
        _home = await NewZoneAsync("home-top");
        _side = await NewZoneAsync("sidebar");
        _summer = await NewBannerAsync(ImageDraft(NewMediaKey(), _home.Key, _side.Key));
    }

    private DailyStatisticImport Row(DateOnly day, long impressions, long clicks, Guid? banner = null, Guid? zone = null) =>
        new() { Day = day, BannerKey = banner ?? _summer.Key, ZoneKey = zone ?? _home.Key, Impressions = impressions, Clicks = clicks };

    private IReadOnlyList<DailyStatRow> Stored(DateOnly from, DateOnly to)
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return GetRequiredService<IStatisticsRepository>().GetDailyRows(from, to, null, null);
    }

    [Test]
    public async Task NewRows_AreInserted_AndMarkedImported()
    {
        BannerImportResult result = await Import.ImportStatisticsAsync([Row(March1, 100, 3), Row(March1.AddDays(1), 40, 1)], StatisticImportMode.Add);

        Assert.That((result.Inserted, result.Updated, result.Rejected), Is.EqualTo((2, 0, 0)));
        Assert.That(
            Stored(March1, March1.AddDays(1)).Select(r => (r.Day, r.Impressions, r.Clicks, r.IsImported)),
            Is.EqualTo(new[] { (March1, 100L, 3L, true), (March1.AddDays(1), 40L, 1L, true) }));
    }

    [Test]
    public async Task Add_AddsToAnExistingRow()
    {
        await Import.ImportStatisticsAsync([Row(March1, 100, 3)], StatisticImportMode.Add);

        BannerImportResult result = await Import.ImportStatisticsAsync([Row(March1, 20, 2)], StatisticImportMode.Add);

        Assert.That((result.Inserted, result.Updated, result.Rejected), Is.EqualTo((0, 1, 0)));
        DailyStatRow row = Stored(March1, March1).Single();
        Assert.That((row.Impressions, row.Clicks, row.IsImported), Is.EqualTo((120L, 5L, true)));
    }

    [Test]
    public async Task Replace_OverwritesTheCounts()
    {
        await Import.ImportStatisticsAsync([Row(March1, 100, 3)], StatisticImportMode.Add);

        BannerImportResult result = await Import.ImportStatisticsAsync([Row(March1, 20, 2)], StatisticImportMode.Replace);

        Assert.That((result.Inserted, result.Updated, result.Rejected), Is.EqualTo((0, 1, 0)));
        DailyStatRow row = Stored(March1, March1).Single();
        Assert.That((row.Impressions, row.Clicks, row.IsImported), Is.EqualTo((20L, 2L, true)));
    }

    [Test]
    public async Task ImportOverACountedRow_MarksItImported()
    {
        using (IScope scope = ScopeProvider.CreateScope())
        {
            scope.Database.Insert(new DailyStatDto { Day = March1, BannerId = _summer.Id, ZoneId = _home.Id, Impressions = 10, Clicks = 1, IsImported = false });
            scope.Complete();
        }

        await Import.ImportStatisticsAsync([Row(March1, 5, 0)], StatisticImportMode.Add);

        DailyStatRow row = Stored(March1, March1).Single();
        Assert.That((row.Impressions, row.Clicks, row.IsImported), Is.EqualTo((15L, 1L, true)));
    }

    [Test]
    public async Task UnknownKeysAndNegativeNumbers_AreRejectedAndCounted_TheRestIsImported()
    {
        DailyStatisticImport[] rows =
        [
            Row(March1, 10, 1),
            Row(March1, 10, 1, banner: Guid.NewGuid()),
            Row(March1, 10, 1, zone: Guid.NewGuid()),
            Row(March1, -1, 0, zone: _side.Key),
            Row(March1, 5, -1, zone: _side.Key),
        ];

        BannerImportResult result = await Import.ImportStatisticsAsync(rows, StatisticImportMode.Add);

        Assert.That((result.Inserted, result.Updated, result.Rejected), Is.EqualTo((1, 0, 4)));
        Assert.That(Stored(March1, March1), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MoreThanOneBatch_IsImportedInFull()
    {
        // 2,500 rows: three transactions of at most 1,000 rows.
        DailyStatisticImport[] rows = [.. Enumerable.Range(0, 2_500).Select(i => Row(March1.AddDays(i), i + 1, 0))];

        BannerImportResult result = await Import.ImportStatisticsAsync(rows, StatisticImportMode.Add);

        Assert.That((result.Inserted, result.Updated, result.Rejected), Is.EqualTo((2_500, 0, 0)));
        Assert.That(Stored(March1, March1.AddDays(2_499)), Has.Count.EqualTo(2_500));
    }

    [Test]
    public async Task AReplaceImportThatLowersTheTotals_BringsALimitedBannerBack()
    {
        BannerDraftModel draft = ImageDraft(NewMediaKey(), _home.Key);
        draft.ImpressionLimit = 100;
        Banner limited = await NewBannerAsync(draft);
        Assert.That((await BannerService.PublishAsync(limited.Key, User)).Success, Is.True);
        await Import.ImportStatisticsAsync([Row(March1, 150, 0, banner: limited.Key)], StatisticImportMode.Add);
        await Snapshot.RebuildAsync();
        Assert.That(Snapshot.Current.Zones["home-top"].Candidates.Any(c => c.BannerId == limited.Id), Is.False, "over its limit");

        await Import.ImportStatisticsAsync([Row(March1, 20, 0, banner: limited.Key)], StatisticImportMode.Replace);

        Assert.That(Snapshot.Current.Zones["home-top"].Candidates.Any(c => c.BannerId == limited.Id), Is.True, "the Replace import rebuilt the snapshot");
    }

    [Test]
    public void NullRows_Throw() =>
        Assert.ThrowsAsync<ArgumentNullException>(() => Import.ImportStatisticsAsync(null!, StatisticImportMode.Add));
}
