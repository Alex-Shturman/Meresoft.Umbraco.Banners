using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Tokens;

/// <summary>View tokens protected with ASP.NET Core data protection, limited in time by <c>ViewTokenLifetime</c>.</summary>
public sealed class ViewTokenProtector : IViewTokenProtector
{
    private const int NonceLength = 12;
    private const int HeaderLength = 1 + 4 + 1;

    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeSpan _lifetime;

    /// <summary>Creates the protector.</summary>
    public ViewTokenProtector(IDataProtectionProvider provider, IOptions<BannersOptions> options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        _protector = provider.CreateProtector("Meresoft.Banners.ViewToken.v1").ToTimeLimitedDataProtector();
        _lifetime = options.Value.ViewTokenLifetime;
    }

    /// <inheritdoc />
    public string Issue(int zoneId, IReadOnlyList<int> bannerIds)
    {
        ArgumentNullException.ThrowIfNull(bannerIds);
        if (bannerIds.Count is 0 or > byte.MaxValue)
        {
            throw new ArgumentException("A token holds between 1 and 255 banners.", nameof(bannerIds));
        }

        byte[] buffer = new byte[HeaderLength + (4 * bannerIds.Count) + NonceLength];
        Span<byte> span = buffer;
        span[0] = ViewTokenPayload.CurrentVersion;
        BinaryPrimitives.WriteInt32LittleEndian(span[1..], zoneId);
        span[5] = (byte)bannerIds.Count;
        for (int i = 0; i < bannerIds.Count; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[(HeaderLength + (4 * i))..], bannerIds[i]);
        }

        RandomNumberGenerator.Fill(span[^NonceLength..]);

        return WebEncoders.Base64UrlEncode(_protector.Protect(buffer, _lifetime));
    }

    /// <inheritdoc />
    public bool TryValidate(string? token, [NotNullWhen(true)] out ViewTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        try
        {
            byte[] data = _protector.Unprotect(WebEncoders.Base64UrlDecode(token), out _);
            ReadOnlySpan<byte> span = data;
            if (span.Length < HeaderLength + NonceLength || span[0] != ViewTokenPayload.CurrentVersion)
            {
                return false;
            }

            int count = span[5];
            if (count == 0 || span.Length != HeaderLength + (4 * count) + NonceLength)
            {
                return false;
            }

            var ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                ids[i] = BinaryPrimitives.ReadInt32LittleEndian(span[(HeaderLength + (4 * i))..]);
            }

            payload = new ViewTokenPayload(span[0], BinaryPrimitives.ReadInt32LittleEndian(span[1..]), ids, span[^NonceLength..].ToArray());
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}
