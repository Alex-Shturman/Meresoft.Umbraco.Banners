using System.Globalization;
using System.Text;
using Meresoft.Umbraco.Banners.Services.Models;

namespace Meresoft.Umbraco.Banners.Statistics;

/// <summary>One CSV line: a day of one banner in one zone. Null names stand for a deleted banner or zone.</summary>
public readonly record struct CsvReportRow(DateOnly Day, string? ZoneAlias, string? BannerName, long Impressions, long Clicks);

/// <summary>Writes the report as CSV (contracts/management-api.md). Text that a spreadsheet could run as a formula is defused.</summary>
public static class CsvReportWriter
{
    private const string LineEnd = "\r\n";

    /// <summary>The header and one line per row.</summary>
    public static string Write(IEnumerable<CsvReportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var csv = new StringBuilder("date,zone,banner,impressions,clicks").Append(LineEnd);
        foreach (CsvReportRow row in rows)
        {
            csv.Append(row.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(Field(row.ZoneAlias ?? ReportNames.DeletedZone)).Append(',')
                .Append(Field(row.BannerName ?? ReportNames.DeletedBanner)).Append(',')
                .Append(row.Impressions.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Clicks.ToString(CultureInfo.InvariantCulture)).Append(LineEnd);
        }

        return csv.ToString();
    }

    private static string Field(string value)
    {
        // A leading apostrophe stops spreadsheets from reading the text as a formula (CSV injection).
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
