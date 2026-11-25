using System.Text.RegularExpressions;

namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Validation rules of a zone (data-model.md). Shared by the API and the import service.</summary>
public static partial class ZoneValidator
{
    private const int MaxAliasLength = 100;
    private const int MaxNameLength = 255;
    private const int MaxDescriptionLength = 1000;
    private const int MaxImageSize = 4000;

    [GeneratedRegex("^[a-z][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex AliasPattern();

    /// <summary>Validates a zone. The alias is normalised before it is checked; the zone is not changed.</summary>
    public static FieldValidationResult Validate(BannerZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var result = new FieldValidationResult();

        string alias = AliasNormalizer.Normalize(zone.Alias);
        if (alias.Length is 0 or > MaxAliasLength || !AliasPattern().IsMatch(alias))
        {
            result.Add("alias", "The alias must be 1 to 100 characters: lowercase letters, digits, '-' and '_', starting with a letter.");
        }

        if (string.IsNullOrWhiteSpace(zone.Name) || zone.Name.Length > MaxNameLength)
        {
            result.Add("name", "The name is required and can be at most 255 characters.");
        }

        if (zone.Description is { Length: > MaxDescriptionLength })
        {
            result.Add("description", "The description can be at most 1000 characters.");
        }

        if (zone.ShowCount is < 1 or > 10)
        {
            result.Add("showCount", "The number of banners shown must be 1 to 10.");
        }

        if (zone.ImageWidth is < 1 or > MaxImageSize)
        {
            result.Add("imageWidth", "The image width must be 1 to 4000.");
        }

        if (zone.ImageHeight is < 1 or > MaxImageSize)
        {
            result.Add("imageHeight", "The image height must be 1 to 4000.");
        }

        if (!Enum.IsDefined(zone.RenderMode))
        {
            result.Add("renderMode", "The render mode must be server or client.");
        }

        return result;
    }
}
