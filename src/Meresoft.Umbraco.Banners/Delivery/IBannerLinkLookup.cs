namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>Where a click on a banner goes.</summary>
/// <param name="LinkUrl">The stored target address of the live version.</param>
/// <param name="Countable">True when the banner is published and assigned to the zone of the click, so the click is counted.</param>
public sealed record ClickTarget(string LinkUrl, bool Countable);

/// <summary>Finds the stored link of a banner for the click endpoint.</summary>
/// <remarks>Public for dependency injection only; not part of the stable API and not covered by SemVer.</remarks>
public interface IBannerLinkLookup
{
    /// <summary>
    /// The target of a click, or null when the banner does not exist, has no live version, is an HTML banner or has no valid link.
    /// Looks in the live snapshot first; older banners (unpublished, over a limit) come from a short lived cache over the database.
    /// </summary>
    ClickTarget? Find(int bannerId, int? zoneId);
}
