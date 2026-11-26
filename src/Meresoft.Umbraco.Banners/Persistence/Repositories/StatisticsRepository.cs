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

    /// <summary>The Day column as a query parameter: every statement here goes through <see cref="StatisticsDayParameter"/>.</summary>
    private object DayParameter(DateOnly day) =>
        StatisticsDayParameter.For(Syntax.ProviderName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase), day);

    private string Column(string name) => Syntax.GetQuotedColumnName(name);

    public void AddDelta(CounterEntry delta)
    {
        // Add to the stored value instead of replacing it: several servers write into the same rows (research R7).
        // The query builder cannot express "column = column + value", so these statements are written out, with quoted names.
        string table = Syntax.GetQuotedTableName(DailyStatDto.TableName);
        object day = DayParameter(delta.Day);
        int updated = Database.Execute(
            $"UPDATE {table} SET {Column("Impressions")} = {Column("Impressions")} + @0, {Column("Clicks")} = {Column("Clicks")} + @1 "
            + $"WHERE {Column("Day")} = @2 AND {Column("BannerId")} = @3 AND {Column("ZoneId")} = @4",
            delta.Impressions,
            delta.Clicks,
            day,
            delta.BannerId,
            delta.ZoneId);

        if (updated == 0)
        {
            // A primary key violation here means another server inserted the row first; the caller retries, and the update then succeeds.
            Database.Execute(
                $"INSERT INTO {table} ({Column("Day")}, {Column("BannerId")}, {Column("ZoneId")}, {Column("Impressions")}, {Column("Clicks")}, {Column("IsImported")}) "
                + "VALUES (@0, @1, @2, @3, @4, @5)",
                day,
                delta.BannerId,
                delta.ZoneId,
                delta.Impressions,
                delta.Clicks,
                false);
        }
    }

    public bool Import(DateOnly day, int bannerId, int zoneId, long impressions, long clicks, bool replace)
    {
        string table = Syntax.GetQuotedTableName(DailyStatDto.TableName);
        object dayValue = DayParameter(day);
        string impressionsValue = replace ? "@0" : $"{Column("Impressions")} + @0";
        string clicksValue = replace ? "@1" : $"{Column("Clicks")} + @1";
        int updated = Database.Execute(
            $"UPDATE {table} SET {Column("Impressions")} = {impressionsValue}, {Column("Clicks")} = {clicksValue}, {Column("IsImported")} = @2 "
            + $"WHERE {Column("Day")} = @3 AND {Column("BannerId")} = @4 AND {Column("ZoneId")} = @5",
            impressions,
            clicks,
            true,
            dayValue,
            bannerId,
            zoneId);
        if (updated > 0)
        {
            return false;
        }

        Database.Execute(
            $"INSERT INTO {table} ({Column("Day")}, {Column("BannerId")}, {Column("ZoneId")}, {Column("Impressions")}, {Column("Clicks")}, {Column("IsImported")}) "
            + "VALUES (@0, @1, @2, @3, @4, @5)",
            dayValue,
            bannerId,
            zoneId,
            impressions,
            clicks,
            true);
        return true;
    }

    public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId)
    {
        var sql = NewSql().Select<DailyStatDto>().From<DailyStatDto>()
            .Where($"{Column("Day")} >= @0 AND {Column("Day")} <= @1", DayParameter(from), DayParameter(to));
        if (zoneId is { } zone)
        {
            sql = sql.Where<DailyStatDto>(s => s.ZoneId == zone);
        }

        if (bannerId is { } banner)
        {
            sql = sql.Where<DailyStatDto>(s => s.BannerId == banner);
        }

        // Read through a plain row type: the Day column comes back as text (SQLite) or a datetime (SQL Server).
        return Database.Fetch<DailyStatReadRow>(sql.OrderBy<DailyStatDto>(s => s.Day))
            .Select(d => new DailyStatRow(StatisticsDayParameter.Read(d.Day), d.BannerId, d.ZoneId, d.Impressions, d.Clicks, d.IsImported))
            .ToList();
    }

    public IReadOnlyDictionary<int, BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds)
    {
        if (bannerIds.Count == 0)
        {
            return new Dictionary<int, BannerTotals>();
        }

        // Summed by the database, so a banner with a year of rows costs one result row, not 366 times its zones.
        string table = Syntax.GetQuotedTableName(DailyStatDto.TableName);
        var totals = new Dictionary<int, BannerTotals>();
        foreach (int[] part in bannerIds.Distinct().Chunk(1_000))
        {
            foreach (TotalsRow row in Database.Fetch<TotalsRow>(
                $"SELECT {Column("BannerId")} AS BannerId, SUM({Column("Impressions")}) AS Impressions, SUM({Column("Clicks")}) AS Clicks "
                + $"FROM {table} WHERE {Column("BannerId")} IN (@0) GROUP BY {Column("BannerId")}",
                part))
            {
                totals[row.BannerId] = new BannerTotals(row.Impressions, row.Clicks);
            }
        }

        return totals;
    }

    private sealed class TotalsRow
    {
        public int BannerId { get; set; }

        public long Impressions { get; set; }

        public long Clicks { get; set; }
    }

    /// <summary>A statistics row as read from the database, with the Day value left as the driver returns it.</summary>
    private sealed class DailyStatReadRow
    {
        public object Day { get; set; } = null!;

        public int BannerId { get; set; }

        public int ZoneId { get; set; }

        public long Impressions { get; set; }

        public long Clicks { get; set; }

        public bool IsImported { get; set; }
    }
}
