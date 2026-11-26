using System.Globalization;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>
/// Turns a UTC day into a query parameter for the Day column of the statistics table without relying on driver support
/// for <see cref="DateOnly"/>. SQLite keeps the date as ISO text, SQL Server compares a datetime at midnight with its date column,
/// so in both the stored value is a date with no time part.
/// </summary>
internal static class StatisticsDayParameter
{
    private const string IsoFormat = "yyyy-MM-dd";

    /// <summary>The parameter value for the day.</summary>
    public static object For(bool isSqlite, DateOnly day) =>
        isSqlite ? day.ToString(IsoFormat, CultureInfo.InvariantCulture) : day.ToDateTime(TimeOnly.MinValue);

    /// <summary>Reads a Day value as a database returns it: ISO text (SQLite), a datetime (SQL Server) or a date.</summary>
    public static DateOnly Read(object value) => value switch
    {
        DateOnly day => day,
        DateTime dateTime => DateOnly.FromDateTime(dateTime),
        string text => DateOnly.ParseExact(text[..10], IsoFormat, CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"The Day column returned a {value.GetType().Name}, which is not a date."),
    };
}
