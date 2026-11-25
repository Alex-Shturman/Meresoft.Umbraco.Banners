namespace Meresoft.Umbraco.Banners.Tokens;

/// <summary>What a view token carries: the zone, the banners rendered in it and a random nonce. No visitor data.</summary>
public sealed record ViewTokenPayload(byte Version, int ZoneId, IReadOnlyList<int> BannerIds, byte[] Nonce)
{
    /// <summary>The token format version written today.</summary>
    public const byte CurrentVersion = 1;
}
