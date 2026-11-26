namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>
/// The one rule for a banner's show window, used by the selector and by the state flags: the window is
/// [start, end), so the start moment is inside and the end moment is outside. A missing end is open.
/// </summary>
internal static class Schedule
{
    /// <summary>True when <paramref name="nowUtc"/> is at or after the start, or there is no start.</summary>
    public static bool HasStarted(DateTime? startUtc, DateTime nowUtc) => startUtc is not { } start || nowUtc >= start;

    /// <summary>True when <paramref name="nowUtc"/> is at or after the end; false when there is no end.</summary>
    public static bool HasEnded(DateTime? endUtc, DateTime nowUtc) => endUtc is { } end && nowUtc >= end;

    /// <summary>True when the window has started and has not ended.</summary>
    public static bool IsActive(DateTime? startUtc, DateTime? endUtc, DateTime nowUtc) =>
        HasStarted(startUtc, nowUtc) && !HasEnded(endUtc, nowUtc);
}
