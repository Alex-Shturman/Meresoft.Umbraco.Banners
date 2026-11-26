namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>The package script served from the fixed static path of the Razor class library.</summary>
public static class PackageScript
{
    /// <summary>Path of the script on the site.</summary>
    public const string Path = "/meresoft-banners/banners.js";

    /// <summary>The script tag written once per page.</summary>
    public const string Tag = "<script src=\"" + Path + "\" defer></script>";

    /// <summary>The script tag for a site in a virtual directory: <paramref name="pathBase"/> (already URL encoded, or empty) in front of <see cref="Path"/>. HTML encoded.</summary>
    public static string TagFor(string pathBase) =>
        pathBase.Length == 0 ? Tag : "<script src=\"" + System.Net.WebUtility.HtmlEncode(pathBase + Path) + "\" defer></script>";
}
