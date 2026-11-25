using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Configuration;

/// <summary>Checks <see cref="BannersOptions"/> at start; every message names the failing key.</summary>
public sealed class BannersOptionsValidator : IValidateOptions<BannersOptions>
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinFlush = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxFlush = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MinOneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxOneHour = TimeSpan.FromMinutes(60);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, BannersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        ValidatePublicPath(options.PublicPath, failures);

        if (options.FlushInterval < MinFlush || options.FlushInterval > MaxFlush)
        {
            failures.Add(Message("FlushInterval", "must be between 5 seconds and 1 minute"));
        }

        if (options.ViewTokenLifetime < MinOneMinute || options.ViewTokenLifetime > MaxOneHour)
        {
            failures.Add(Message("ViewTokenLifetime", "must be between 1 and 60 minutes"));
        }

        // Zero disables the periodic reload.
        if (options.CacheSafetyReloadInterval != TimeSpan.Zero
            && (options.CacheSafetyReloadInterval < MinOneMinute || options.CacheSafetyReloadInterval > MaxOneHour))
        {
            failures.Add(Message("CacheSafetyReloadInterval", "must be 0 (disabled) or between 1 and 60 minutes"));
        }

        ValidateBotPatterns(options.Bots?.AdditionalUserAgentPatterns, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateBotPatterns(string[]? patterns, List<string> failures)
    {
        const string key = "Bots:AdditionalUserAgentPatterns";
        if (patterns is null)
        {
            failures.Add(Message(key, "must be a list of non-empty text values"));
            return;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            string pattern = patterns[i];
            if (string.IsNullOrWhiteSpace(pattern))
            {
                failures.Add(Message($"{key}:{i}", "must not be empty"));
                continue;
            }

            // A pattern that cannot be compiled must stop the site at start, not fail on a visitor's request.
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeout);
            }
            catch (ArgumentException)
            {
                failures.Add(Message($"{key}:{i}", "must be a valid pattern"));
            }
        }
    }

    private static void ValidatePublicPath(string? path, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !path.StartsWith('/')
            || path.Length == 1
            || path.EndsWith('/'))
        {
            failures.Add(Message("PublicPath", "must start with '/' and must not end with '/'"));
            return;
        }

        if (path.Equals("/umbraco", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/umbraco/", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(Message("PublicPath", "must not start with '/umbraco'"));
        }
    }

    private static string Message(string key, string rule) => $"{BannersOptions.SectionName}:{key} {rule}.";
}
