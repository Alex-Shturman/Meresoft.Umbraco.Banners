namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Validation rules of banner versions and names (data-model.md). Shared by the API and the import service.</summary>
public static class BannerVersionValidator
{
    private const int MaxNameLength = 255;

    /// <summary>Validates the content of a version for the given banner type.</summary>
    public static FieldValidationResult Validate(BannerType type, BannerVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var result = new FieldValidationResult();

        if (!Enum.IsDefined(type))
        {
            result.Add("type", "The banner type must be image or html.");
            return result;
        }

        bool isImage = type == BannerType.Image;

        if (isImage && (version.MediaKey is null || version.MediaKey == Guid.Empty))
        {
            result.Add("mediaKey", "An image banner needs an image.");
        }

        if (!isImage && string.IsNullOrWhiteSpace(version.Html))
        {
            result.Add("html", "An html banner needs html.");
        }

        if (string.IsNullOrWhiteSpace(version.LinkUrl))
        {
            if (isImage)
            {
                result.Add("linkUrl", "An image banner needs a link.");
            }
        }
        else if (!IsAbsoluteHttpUrl(version.LinkUrl))
        {
            result.Add("linkUrl", "The link must be an absolute http or https address of at most 2048 characters.");
        }

        if (isImage && string.IsNullOrWhiteSpace(version.AltText))
        {
            result.Add("altText", "An image banner needs alternative text.");
        }

        if (version.AltText is { Length: > MaxNameLength })
        {
            result.Add("altText", "The alternative text can be at most 255 characters.");
        }

        if (version.Title is { Length: > MaxNameLength })
        {
            result.Add("title", "The title can be at most 255 characters.");
        }

        if (version.Weight is < 1 or > 100)
        {
            result.Add("weight", "The weight must be 1 to 100.");
        }

        if (version.StartUtc is { } start && version.EndUtc is { } end && end <= start)
        {
            result.Add("endUtc", "The end must be after the start.");
        }

        if (version.ImpressionLimit is < 1)
        {
            result.Add("impressionLimit", "The impression limit must be a whole number, 1 or more.");
        }

        if (version.ClickLimit is < 1)
        {
            result.Add("clickLimit", "The click limit must be a whole number, 1 or more.");
        }
        else if (!isImage && version.ClickLimit is not null)
        {
            result.Add("clickLimit", "Clicks of html banners are not counted, so they have no click limit.");
        }

        return result;
    }

    /// <summary>Validates the name of a banner.</summary>
    public static FieldValidationResult ValidateName(string? name)
    {
        var result = new FieldValidationResult();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            result.Add("name", "The name is required and can be at most 255 characters.");
        }

        return result;
    }

    /// <summary>The type of a banner is fixed after creation.</summary>
    public static bool IsTypeChangeAllowed(BannerType existing, BannerType requested) => existing == requested;

    private static bool IsAbsoluteHttpUrl(string url) => LinkUrlRules.IsAbsoluteHttpUrl(url);
}
