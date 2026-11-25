namespace Meresoft.Umbraco.Banners.Configuration;

/// <summary>Settings of the package, bound from the <c>Meresoft:Banners</c> section.</summary>
public sealed class BannersOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Meresoft:Banners";

    /// <summary>Base path of the public endpoints (click, seen, zone delivery).</summary>
    public string PublicPath { get; set; } = "/_banners";

    /// <summary>False disables the client mode delivery endpoint.</summary>
    public bool DeliveryEndpointEnabled { get; set; } = true;

    /// <summary>How often counted impressions and clicks are written to the database.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>How long after rendering an impression can still be reported.</summary>
    public TimeSpan ViewTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Period of the safety rebuild of the banner snapshot; zero disables it.</summary>
    public TimeSpan CacheSafetyReloadInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Bot filter settings.</summary>
    public BotOptions Bots { get; set; } = new();
}

/// <summary>Bot filter settings.</summary>
public sealed class BotOptions
{
    /// <summary>Case-insensitive user agent substrings added to the shipped list.</summary>
    public string[] AdditionalUserAgentPatterns { get; set; } = [];
}
