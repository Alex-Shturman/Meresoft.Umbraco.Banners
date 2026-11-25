using Meresoft.Umbraco.Banners.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.ManagementApi;

/// <summary>Maps service results to RFC 9457 problem details (contracts/management-api.md).</summary>
public static class OperationStatusResults
{
    /// <summary>Problem type used when an HTML banner is changed without the HTML permission.</summary>
    public const string HtmlPermissionRequiredType = "html-permission-required";

    /// <summary>Turns a failed status into a problem response.</summary>
    /// <param name="status">Any status except <see cref="BannerOperationStatus.Success"/>.</param>
    /// <param name="errors">Error text per field, for invalid data.</param>
    /// <param name="bannerKeys">Banners that block a zone delete.</param>
    public static IActionResult ToProblem(
        BannerOperationStatus status,
        IReadOnlyDictionary<string, string>? errors = null,
        IReadOnlyList<Guid>? bannerKeys = null)
    {
        switch (status)
        {
            case BannerOperationStatus.Success:
                throw new ArgumentException("A successful status is not a problem.", nameof(status));

            case BannerOperationStatus.NotFound:
                return Problem(StatusCodes.Status404NotFound, "not-found", "Not found", "The banner or zone does not exist.");

            case BannerOperationStatus.InvalidData:
                var validation = new ValidationProblemDetails(
                    (errors ?? new Dictionary<string, string>()).ToDictionary(e => e.Key, e => new[] { e.Value }))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Type = "invalid-data",
                    Title = "Invalid data",
                    Detail = "One or more fields are not valid.",
                };
                return new ObjectResult(validation) { StatusCode = StatusCodes.Status400BadRequest };

            case BannerOperationStatus.NoZoneAssigned:
                return Problem(StatusCodes.Status400BadRequest, "no-zone-assigned", "No zone assigned", "Assign at least one zone before publishing.");

            case BannerOperationStatus.TypeChangeNotAllowed:
                return Problem(StatusCodes.Status400BadRequest, "type-change-not-allowed", "Type cannot change", "The type of a banner cannot be changed after it is created.");

            case BannerOperationStatus.DuplicateAlias:
                return Problem(StatusCodes.Status409Conflict, "alias-taken", "Alias taken", "Another zone already uses this alias.");

            case BannerOperationStatus.ZoneInUse:
                ObjectResult inUse = Problem(StatusCodes.Status409Conflict, "zone-in-use", "Zone in use", "Banners are still assigned to this zone.");
                ((ProblemDetails)inUse.Value!).Extensions["bannerKeys"] = bannerKeys ?? [];
                return inUse;

            case BannerOperationStatus.ChangedByAnotherUser:
                return Problem(StatusCodes.Status409Conflict, "changed-by-another-user", "Changed by another user", "The item was changed after you loaded it. Reload and try again.");

            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status.");
        }
    }

    /// <summary>403 with the type <c>html-permission-required</c> (FR-006).</summary>
    public static IActionResult HtmlPermissionRequired() =>
        Problem(StatusCodes.Status403Forbidden, HtmlPermissionRequiredType, "HTML permission required", "Changing HTML banners needs the HTML banner permission.");

    private static ObjectResult Problem(int status, string type, string title, string detail) =>
        new(new ProblemDetails { Status = status, Type = type, Title = title, Detail = detail }) { StatusCode = status };
}
