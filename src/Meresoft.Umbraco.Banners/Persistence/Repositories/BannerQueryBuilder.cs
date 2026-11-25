using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>
/// Builds the banner list query. The joins, the state conditions and the paging need aliases and
/// <c>COALESCE</c>, which the Umbraco SQL builder cannot express, so the statement is assembled
/// here with every table and column name quoted through the SQL syntax of the database in use
/// (never hard coded quotes), which keeps it valid on SQL Server and SQLite.
/// </summary>
internal sealed class BannerQueryBuilder
{
    private const int MaxPageSize = 100;
    private readonly ISqlSyntaxProvider _syntax;

    public BannerQueryBuilder(ISqlSyntaxProvider syntax)
    {
        _syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
    }

    /// <summary>Largest page the list returns.</summary>
    public static int MaximumPageSize => MaxPageSize;

    /// <summary>Normalises the paging of a filter: skip is at least 0, take is 1 to the maximum page size.</summary>
    public static (int Skip, int Take) Page(BannerFilter filter) =>
        (Math.Max(filter.Skip, 0), Math.Clamp(filter.Take, 1, MaxPageSize));

    /// <summary>The count statement and its arguments.</summary>
    public Sql Count(BannerFilter filter)
    {
        (string from, object[] args) = FromAndWhere(filter);
        return new Sql($"SELECT COUNT(*) {from}", args);
    }

    /// <summary>The page statement (banner rows only) and its arguments, ordered newest first.</summary>
    public Sql Select(BannerFilter filter)
    {
        (string from, object[] args) = FromAndWhere(filter);
        string columns = string.Join(", ", new[]
        {
            "Id", "Key", "Name", "Type", "IsPublished", "CreateDate", "UpdateDate", "CreatedBy", "UpdatedBy", "PublishDate",
        }.Select(c => Banner(c)));
        return new Sql($"SELECT {columns} {from} ORDER BY {Banner("UpdateDate")} DESC, {Banner("Id")} DESC", args);
    }

    private (string From, object[] Args) FromAndWhere(BannerFilter filter)
    {
        var where = new List<string>();
        var args = new List<object>();

        if (filter.Type is { } type)
        {
            where.Add($"{Banner("Type")} = @{args.Count}");
            args.Add((byte)type);
        }

        if (filter.ZoneId is { } zoneId)
        {
            where.Add(
                $"EXISTS (SELECT 1 FROM {Table(ZoneAssignmentDto.TableName)} za " +
                $"WHERE {Col("za", "ZoneId")} = @{args.Count} " +
                $"AND {Col("za", "BannerVersionId")} = COALESCE({Live("Id")}, {Draft("Id")}))");
            args.Add(zoneId);
        }

        if (filter.State is { } state)
        {
            where.Add(state switch
            {
                BannerState.Draft => $"{Live("Id")} IS NULL",
                BannerState.Unpublished => $"{Live("Id")} IS NOT NULL AND {Banner("IsPublished")} = 0",
                BannerState.Published =>
                    $"{Live("Id")} IS NOT NULL AND {Banner("IsPublished")} = 1 AND " +
                    $"({Draft("Id")} IS NULL OR {Draft("SavedDate")} = {Live("SavedDate")})",
                BannerState.PublishedWithChanges =>
                    $"{Live("Id")} IS NOT NULL AND {Banner("IsPublished")} = 1 AND " +
                    $"{Draft("Id")} IS NOT NULL AND {Draft("SavedDate")} <> {Live("SavedDate")}",
                _ => throw new ArgumentOutOfRangeException(nameof(filter), "Unknown banner state."),
            });
        }

        string from =
            $"FROM {Table(BannerDto.TableName)} b " +
            $"LEFT JOIN {Table(BannerVersionDto.TableName)} d ON {Draft("BannerId")} = {Banner("Id")} AND {Draft("Kind")} = {(byte)BannerVersionKind.Draft} " +
            $"LEFT JOIN {Table(BannerVersionDto.TableName)} l ON {Live("BannerId")} = {Banner("Id")} AND {Live("Kind")} = {(byte)BannerVersionKind.Live}" +
            (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty);
        return (from, args.ToArray());
    }

    private string Table(string table) => _syntax.GetQuotedTableName(table);

    private string Col(string alias, string column) => $"{alias}.{_syntax.GetQuotedColumnName(column)}";

    private string Banner(string column) => Col("b", column);

    private string Draft(string column) => Col("d", column);

    private string Live(string column) => Col("l", column);
}
