namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>
/// Brings timestamps to a resolution every supported database stores exactly. SQL Server
/// <c>datetime</c> keeps only .000, .003 and .007 second steps, so a value saved with finer
/// precision could read back different and break comparisons such as "draft saved after publish".
/// Multiples of 10 milliseconds are stored exactly.
/// </summary>
public static class UtcTimestamp
{
    private const long ResolutionTicks = TimeSpan.TicksPerMillisecond * 10;

    /// <summary>Converts to UTC and rounds down to whole 10 millisecond steps.</summary>
    public static DateTime Truncate(DateTime value)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return new DateTime(utc.Ticks - (utc.Ticks % ResolutionTicks), DateTimeKind.Utc);
    }

    /// <summary>Same as <see cref="Truncate(DateTime)"/> for nullable values.</summary>
    public static DateTime? Truncate(DateTime? value) => value is null ? null : Truncate(value.Value);
}
