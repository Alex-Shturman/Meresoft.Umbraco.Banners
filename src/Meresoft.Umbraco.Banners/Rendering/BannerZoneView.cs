using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Selection;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>Everything the writer needs to write one zone.</summary>
/// <param name="Alias">Normalised zone alias.</param>
/// <param name="CssClass">Extra classes for the zone element.</param>
/// <param name="Banners">The chosen banners.</param>
/// <param name="FallbackHtml">Developer content shown when there is no banner; null for none.</param>
/// <param name="Mode">Server writes the chosen banners; client writes a placeholder the package script fills.</param>
public sealed record BannerZoneView(
    string Alias,
    string? CssClass,
    IReadOnlyList<SelectedBanner> Banners,
    string? FallbackHtml,
    ZoneRenderMode Mode = ZoneRenderMode.Server);
