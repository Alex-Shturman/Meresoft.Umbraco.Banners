namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Carries the field errors of a failed validation in the <c>Exception</c> of a service attempt.</summary>
public sealed class BannerValidationException : Exception
{
    /// <summary>Creates the exception from a validation result.</summary>
    public BannerValidationException(FieldValidationResult result)
        : base("The banner data is not valid: " + string.Join(", ", (result ?? throw new ArgumentNullException(nameof(result))).Errors.Keys))
    {
        Errors = result.Errors;
    }

    /// <summary>The first error of each failing field.</summary>
    public IReadOnlyDictionary<string, string> Errors { get; }
}
