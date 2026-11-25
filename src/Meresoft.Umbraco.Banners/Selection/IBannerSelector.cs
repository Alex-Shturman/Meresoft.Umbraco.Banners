namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>Chooses the banners to show in a zone, from the in-memory snapshot only.</summary>
public interface IBannerSelector
{
    /// <summary>Up to the zone's show count of different banners; empty when the zone is unknown or has no candidate.</summary>
    IReadOnlyList<SelectedBanner> Select(string zoneAlias);
}
