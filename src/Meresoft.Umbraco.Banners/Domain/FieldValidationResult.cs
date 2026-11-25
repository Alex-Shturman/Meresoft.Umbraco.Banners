namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Outcome of a validation: error text per field name (camel case, as in the API).</summary>
public sealed class FieldValidationResult
{
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    /// <summary>The first error of each failing field.</summary>
    public IReadOnlyDictionary<string, string> Errors => _errors;

    /// <summary>True when nothing failed.</summary>
    public bool IsValid => _errors.Count == 0;

    internal void Add(string field, string message) => _errors.TryAdd(field, message);
}
