namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>The one rule for banner links, used when a banner is saved and again before a click is redirected.</summary>
internal static class LinkUrlRules
{
    /// <summary>The longest link that can be stored.</summary>
    public const int MaxLength = 2048;

    /// <summary>True for an absolute http or https address with a host and at most <see cref="MaxLength"/> characters.</summary>
    public static bool IsAbsoluteHttpUrl(string? url) =>
        url is { Length: > 0 and <= MaxLength }
        && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}
