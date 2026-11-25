namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>The package script served from the fixed static path of the Razor class library.</summary>
public static class PackageScript
{
    /// <summary>Path of the script on the site.</summary>
    public const string Path = "/meresoft-banners/banners.js";

    /// <summary>The script tag written once per page.</summary>
    public const string Tag = "<script src=\"" + Path + "\" defer></script>";
}
