using Meresoft.Umbraco.Banners.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.History;

/// <summary>The paging rules of the history endpoints, in one place.</summary>
internal static class HistoryPaging
{
    /// <summary>The most entries one request returns.</summary>
    public const int MaxTake = 100;

    /// <summary>The default page size.</summary>
    public const int DefaultTake = 50;

    /// <summary>
    /// Null when <paramref name="skip"/> (0 to <see cref="BannerHistoryReader.MaxSkip"/>) and <paramref name="take"/>
    /// (1 to <see cref="MaxTake"/>) are valid; otherwise a 400 that names every bad field, not only the first.
    /// </summary>
    public static IActionResult? Validate(int skip, int take)
    {
        var errors = new Dictionary<string, string>();
        if (skip is < 0 or > BannerHistoryReader.MaxSkip)
        {
            errors["skip"] = $"Skip must be 0 to {BannerHistoryReader.MaxSkip}.";
        }

        if (take is < 1 or > MaxTake)
        {
            errors["take"] = $"Take must be 1 to {MaxTake}.";
        }

        return errors.Count == 0 ? null : OperationStatusResults.ToProblem(BannerOperationStatus.InvalidData, errors);
    }
}
