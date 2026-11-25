namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>A zone alias that a template asked for but that does not exist as a zone yet (FR-003).</summary>
/// <param name="Alias">Normalised alias.</param>
/// <param name="FirstSeen">First time a template asked for it, UTC.</param>
/// <param name="LastSeen">Last time a template asked for it, UTC.</param>
public sealed record RequestedZone(string Alias, DateTime FirstSeen, DateTime LastSeen);
