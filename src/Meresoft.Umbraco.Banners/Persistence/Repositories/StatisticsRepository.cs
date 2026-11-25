using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <inheritdoc cref="IStatisticsRepository" />
internal sealed class StatisticsRepository : RepositoryBase, IStatisticsRepository
{
    public StatisticsRepository(IScopeAccessor scopeAccessor)
        : base(scopeAccessor)
    {
    }

    public void AddDelta(CounterEntry delta)
    {
        // Add to the stored value instead of replacing it: several servers write into the same rows (research R7).
        // The query builder cannot express "column = column + value", so this one statement is written out, with quoted names.
        string table = Syntax.GetQuotedTableName(DailyStatDto.TableName);
        string impressions = Syntax.GetQuotedColumnName("Impressions");
        string clicks = Syntax.GetQuotedColumnName("Clicks");
        int updated = Database.Execute(
            $"UPDATE {table} SET {impressions} = {impressions} + @0, {clicks} = {clicks} + @1 "
            + $"WHERE {Syntax.GetQuotedColumnName("Day")} = @2 AND {Syntax.GetQuotedColumnName("BannerId")} = @3 AND {Syntax.GetQuotedColumnName("ZoneId")} = @4",
            delta.Impressions,
            delta.Clicks,
            delta.Day,
            delta.BannerId,
            delta.ZoneId);

        if (updated == 0)
        {
            // A primary key violation here means another server inserted the row first; the caller retries, and the update then succeeds.
            Database.Insert(new DailyStatDto
            {
                Day = delta.Day,
                BannerId = delta.BannerId,
                ZoneId = delta.ZoneId,
                Impressions = delta.Impressions,
                Clicks = delta.Clicks,
            });
        }
    }

    public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId)
    {
        var sql = NewSql().Select<DailyStatDto>().From<DailyStatDto>()
            .Where<DailyStatDto>(s => s.Day >= from && s.Day <= to);
        if (zoneId is { } zone)
        {
            sql = sql.Where<DailyStatDto>(s => s.ZoneId == zone);
        }

        if (bannerId is { } banner)
        {
            sql = sql.Where<DailyStatDto>(s => s.BannerId == banner);
        }

        return Database.Fetch<DailyStatDto>(sql.OrderBy<DailyStatDto>(s => s.Day))
            .Select(d => new DailyStatRow(d.Day, d.BannerId, d.ZoneId, d.Impressions, d.Clicks, d.IsImported))
            .ToList();
    }

    public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds)
    {
        if (bannerIds.Count == 0)
        {
            return new Dictionary<int, BannerTotals>();
        }

        int[] ids = [.. bannerIds];
        return Database.Fetch<DailyStatDto>(NewSql().Select<DailyStatDto>().From<DailyStatDto>().WhereIn<DailyStatDto>(s => s.BannerId, ids))
            .GroupBy(d => d.BannerId)
            .ToDictionary(g => g.Key, g => new BannerTotals(g.Sum(d => d.Impressions), g.Sum(d => d.Clicks)));
    }
}
