namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>How a zone is written into a page.</summary>
public enum ZoneRenderMode : byte
{
    /// <summary>The banner is chosen and written by the server while the page renders.</summary>
    Server = 0,

    /// <summary>The page holds a placeholder and the browser asks for the banner (cached pages).</summary>
    Client = 1,
}
