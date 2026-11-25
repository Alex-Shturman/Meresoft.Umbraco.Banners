using System.Buffers;
using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>Decides whether a user agent belongs to an automated client, using plain substring patterns.</summary>
public sealed class BotMatcher
{
    private readonly SearchValues<string> _patterns;

    /// <summary>Builds the matcher from the shipped patterns plus the configured additional ones.</summary>
    public BotMatcher(IOptions<BannersOptions> options)
    {
        var patterns = new HashSet<string>(BotPatterns.Shipped, StringComparer.OrdinalIgnoreCase);

        foreach (string? pattern in options.Value.Bots.AdditionalUserAgentPatterns)
        {
            string? trimmed = pattern?.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                patterns.Add(trimmed);
            }
        }

        _patterns = SearchValues.Create(patterns.ToArray(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns true for a missing or blank user agent or one containing a bot pattern; the value is never stored or logged.</summary>
    public bool IsBot(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return true;
        }

        return userAgent.AsSpan().ContainsAny(_patterns);
    }
}
