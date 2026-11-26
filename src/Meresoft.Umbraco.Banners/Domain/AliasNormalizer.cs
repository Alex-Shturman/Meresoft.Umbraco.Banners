namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Brings a zone alias to its stored form: trimmed and lowercase.</summary>
public static class AliasNormalizer
{
    /// <summary>Normalises an alias; null gives an empty string.</summary>
    public static string Normalize(string? alias) => (alias ?? string.Empty).Trim().ToLowerInvariant();
}
