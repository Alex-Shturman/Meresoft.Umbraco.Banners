using Meresoft.Umbraco.Banners.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi.History;

/// <summary>The paging rules of the history endpoints, in one place.</summary>
internal static class HistoryPaging
{
    /// <summary>The default page size.</summary>
    public const int DefaultTake = 50;

    /// <summary>
    /// Null when <paramref name="skip"/> (0 to <see cref="BannerHistoryReader.MaxSkip"/>) and <paramref name="take"/>
    /// (1 to <see cref="BannersConstants.MaxPageSize"/>) are valid; otherwise a 400 that names every bad field, not only the first.
    /// </summary>
    public static IActionResult? Validate(int skip, int take)
    {
        var errors = new Dictionary<string, string>();
        if (skip is < 0 or > BannerHistoryReader.MaxSkip)
        {
            errors["skip"] = $"Skip must be 0 to {BannerHistoryReader.MaxSkip}.";
        }

        if (take is < 1 or > BannersConstants.MaxPageSize)
        {
            errors["take"] = $"Take must be 1 to {BannersConstants.MaxPageSize}.";
        }

        return errors.Count == 0 ? null : OperationStatusResults.ToProblem(BannerOperationStatus.InvalidData, errors);
    }
}
