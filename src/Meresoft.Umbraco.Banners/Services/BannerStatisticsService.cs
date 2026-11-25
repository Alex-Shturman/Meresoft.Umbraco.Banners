using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Statistics;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Services;

/// <inheritdoc cref="IBannerStatisticsService" />
internal sealed class BannerStatisticsService : IBannerStatisticsService
{
    private readonly IScopeProvider _scopeProvider;
    private readonly IStatisticsRepository _statistics;
    private readonly IBannerRepository _banners;
    private readonly IZoneRepository _zones;

    public BannerStatisticsService(IScopeProvider scopeProvider, IStatisticsRepository statistics, IBannerRepository banners, IZoneRepository zones)
    {
        _scopeProvider = scopeProvider;
        _statistics = statistics;
        _banners = banners;
        _zones = zones;
    }

    public Task<Attempt<BannerReport, BannerOperationStatus>> GetReportAsync(BannerReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsValidPeriod(query))
        {
            return Task.FromResult(Attempt.FailWithStatus<BannerReport, BannerOperationStatus>(BannerOperationStatus.InvalidData, null!));
        }

        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        Loaded loaded = Load(query);
        BannerReport report = BuildReport(query, loaded);
        return Task.FromResult(Attempt.SucceedWithStatus(BannerOperationStatus.Success, report));
    }

    public Task<Attempt<string, BannerOperationStatus>> GetCsvAsync(BannerReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsValidPeriod(query))
        {
            return Task.FromResult(Attempt.FailWithStatus<string, BannerOperationStatus>(BannerOperationStatus.InvalidData, null!));
        }

        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        Loaded loaded = Load(query);
        IEnumerable<CsvReportRow> rows = loaded.Rows
            .Select(r => new CsvReportRow(
                r.Day,
                loaded.ZoneAliases.GetValueOrDefault(r.ZoneId),
                loaded.Banners.TryGetValue(r.BannerId, out BannerName? banner) ? banner.Name : null,
                r.Impressions,
                r.Clicks))
            .OrderBy(r => r.Day)
            .ThenBy(r => r.ZoneAlias, StringComparer.Ordinal)
            .ThenBy(r => r.BannerName, StringComparer.Ordinal);
        return Task.FromResult(Attempt.SucceedWithStatus(BannerOperationStatus.Success, CsvReportWriter.Write(rows)));
    }

    private static bool IsValidPeriod(BannerReportQuery query) =>
        query.To >= query.From && query.To.DayNumber - query.From.DayNumber < BannerReportQuery.MaxDays;

    /// <summary>Reads the rows and the names they need. Must run inside a scope.</summary>
    private Loaded Load(BannerReportQuery query)
    {
        int? zoneId = null;
        int? bannerId = null;
        bool unknownFilter = false;
        if (query.ZoneKey is { } zoneKey)
        {
            zoneId = _zones.GetByKey(zoneKey)?.Id;
            unknownFilter |= zoneId is null;
        }

        if (query.BannerKey is { } bannerKey)
        {
            bannerId = _banners.GetByKey(bannerKey)?.Id;
            unknownFilter |= bannerId is null;
        }

        // A filter for something that does not exist (any more) matches nothing.
        IReadOnlyList<DailyStatRow> rows = unknownFilter ? [] : _statistics.GetDailyRows(query.From, query.To, zoneId, bannerId);

        Dictionary<int, string> zoneAliases = _zones.GetAll().ToDictionary(z => z.Id, z => z.Alias);
        var banners = new Dictionary<int, BannerName>();
        foreach (int id in rows.Select(r => r.BannerId).Distinct())
        {
            if (_banners.GetById(id) is { } banner)
            {
                banners[id] = new BannerName(banner.Key, banner.Name);
            }
        }

        return new Loaded(rows, zoneAliases, banners);
    }

    private static BannerReport BuildReport(BannerReportQuery query, Loaded loaded)
    {
        BannerReportRow[] rows = loaded.Rows
            .GroupBy(r => (r.BannerId, r.ZoneId))
            .Select(g =>
            {
                long impressions = g.Sum(r => r.Impressions);
                long clicks = g.Sum(r => r.Clicks);
                bool known = loaded.Banners.TryGetValue(g.Key.BannerId, out BannerName? banner);
                return new BannerReportRow(
                    known ? banner!.Key : null,
                    known ? banner!.Name : ReportNames.DeletedBanner,
                    loaded.ZoneAliases.GetValueOrDefault(g.Key.ZoneId, ReportNames.DeletedZone),
                    impressions,
                    clicks,
                    Ctr.Calculate(impressions, clicks));
            })
            .OrderByDescending(r => r.Impressions)
            .ThenBy(r => r.BannerName, StringComparer.Ordinal)
            .ThenBy(r => r.ZoneAlias, StringComparer.Ordinal)
            .ToArray();

        // Every day of the period is in the series, so a chart has no gaps to guess about.
        Dictionary<DateOnly, ReportDay> byDay = loaded.Rows
            .GroupBy(r => r.Day)
            .ToDictionary(g => g.Key, g => new ReportDay(g.Key, g.Sum(r => r.Impressions), g.Sum(r => r.Clicks), g.Any(r => r.IsImported)));
        var daily = new List<ReportDay>();
        for (DateOnly day = query.From; day <= query.To; day = day.AddDays(1))
        {
            daily.Add(byDay.TryGetValue(day, out ReportDay? found) ? found : new ReportDay(day, 0, 0, false));
        }

        long totalImpressions = rows.Sum(r => r.Impressions);
        long totalClicks = rows.Sum(r => r.Clicks);
        return new BannerReport(new ReportTotals(totalImpressions, totalClicks, Ctr.Calculate(totalImpressions, totalClicks)), rows, daily);
    }

    private sealed record BannerName(Guid Key, string Name);

    private sealed record Loaded(IReadOnlyList<DailyStatRow> Rows, Dictionary<int, string> ZoneAliases, Dictionary<int, BannerName> Banners);
}
