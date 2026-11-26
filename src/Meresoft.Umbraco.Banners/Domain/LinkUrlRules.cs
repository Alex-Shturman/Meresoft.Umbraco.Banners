namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>The one rule for banner links, used when a banner is saved and again before a click is redirected.</summary>
internal static class LinkUrlRules
{
    /// <summary>The longest link that can be stored.</summary>
    public const int MaxLength = 2048;

    /// <summary>
    /// True for an absolute http or https address with a host and at most <see cref="MaxLength"/> characters, without any
    /// control character or whitespace (the browser would strip or reject them, and they can split a header).
    /// </summary>
    public static bool IsAbsoluteHttpUrl(string? url) =>
        url is { Length: > 0 and <= MaxLength }
        && !url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c))
        && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>The normalised form (<see cref="Uri.AbsoluteUri"/>) of an address that passed <see cref="IsAbsoluteHttpUrl"/>, or null when it did not.</summary>
    public static string? Normalize(string? url) =>
        IsAbsoluteHttpUrl(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.AbsoluteUri : null;
}
