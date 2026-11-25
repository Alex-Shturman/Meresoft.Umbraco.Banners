using System.Diagnostics.CodeAnalysis;

namespace Meresoft.Umbraco.Banners.Tokens;

/// <summary>Issues and validates the signed, expiring token that proves a banner was rendered.</summary>
public interface IViewTokenProtector
{
    /// <summary>Creates a token for the banners rendered in a zone.</summary>
    string Issue(int zoneId, IReadOnlyList<int> bannerIds);

    /// <summary>Checks a token. Never throws: any bad, tampered or expired token gives false.</summary>
    bool TryValidate(string? token, [NotNullWhen(true)] out ViewTokenPayload? payload);
}
