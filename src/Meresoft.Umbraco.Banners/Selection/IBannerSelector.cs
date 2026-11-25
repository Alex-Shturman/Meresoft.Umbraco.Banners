namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>Chooses the banners to show in a zone, from the in-memory snapshot only.</summary>
public interface IBannerSelector
{
    /// <summary>
    /// Up to the zone's show count of different banners; empty when the zone is unknown or has no candidate.
    /// The alias must already be normalised (<c>AliasNormalizer.Normalize</c>: trimmed and lowercase).
    /// </summary>
    IReadOnlyList<SelectedBanner> Select(string zoneAlias);
}
