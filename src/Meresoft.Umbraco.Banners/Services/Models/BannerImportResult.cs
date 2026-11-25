namespace Meresoft.Umbraco.Banners.Services.Models;

/// <summary>What a statistics import did.</summary>
/// <param name="Inserted">Rows that did not exist and were created.</param>
/// <param name="Updated">Rows that existed and were changed.</param>
/// <param name="Rejected">Rows that were skipped: unknown banner or zone key, or a negative number.</param>
public sealed record BannerImportResult(int Inserted, int Updated, int Rejected);
