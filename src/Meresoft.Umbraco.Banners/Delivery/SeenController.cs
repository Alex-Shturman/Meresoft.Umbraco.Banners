using System.Buffers;
using System.Text;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// <c>POST {PublicPath}/seen</c>: the package script reports banners that were visible for a second. A line counts as
/// one impression when its view token is valid, lists the banner, was not used before and the request is not from a bot.
/// Everything else is ignored silently, so the answer never tells which report counted. No cookies are read or set.
/// </summary>
[BannersPublicRoute]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class SeenController : ControllerBase
{
    /// <summary>Largest accepted body in bytes.</summary>
    public const int MaxBodyBytes = 16 * 1024;

    private readonly IViewTokenProtector _tokens;
    private readonly ReplaySet _replay;
    private readonly BotMatcher _bots;
    private readonly CounterBuffer _buffer;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the controller.</summary>
    public SeenController(IViewTokenProtector tokens, ReplaySet replay, BotMatcher bots, CounterBuffer buffer, TimeProvider timeProvider)
    {
        _tokens = tokens;
        _replay = replay;
        _bots = bots;
        _buffer = buffer;
        _timeProvider = timeProvider;
    }

    /// <summary>Counts the reported impressions.</summary>
    [Route("seen")]
    public async Task<IActionResult> Seen(CancellationToken cancellationToken)
    {
        // Handled here, not by a verb attribute, so the 405 carries the same headers as every other answer.
        if (!HttpMethods.IsPost(Request.Method))
        {
            Response.Headers.Allow = "POST";
            return StatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        if (Request.ContentLength > MaxBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        string? body = await ReadBodyAsync(cancellationToken);
        if (body is null)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        // The user agent is only read here, in memory, for the bot check.
        if (_bots.IsBot(Request.Headers.UserAgent.ToString()))
        {
            return NoContent();
        }

        DateOnly today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var validated = new Dictionary<string, ViewTokenPayload?>(StringComparer.Ordinal);
        foreach (SeenLine line in SeenBodyParser.Parse(body))
        {
            if (!validated.TryGetValue(line.Token, out ViewTokenPayload? payload))
            {
                payload = _tokens.TryValidate(line.Token, out ViewTokenPayload? valid) ? valid : null;
                validated[line.Token] = payload;
            }

            if (payload is not null && PositionOf(payload.BannerIds, line.BannerId) is var position and >= 0 && _replay.TryAdd(payload.Nonce, position))
            {
                _buffer.AddImpression(today, line.BannerId, payload.ZoneId);
            }
        }

        return NoContent();
    }

    /// <summary>The index of the banner in the token's banner list (the replay set keys a bit on it), or -1 when the token does not carry it.</summary>
    private static int PositionOf(IReadOnlyList<int> bannerIds, int bannerId)
    {
        for (int i = 0; i < bannerIds.Count; i++)
        {
            if (bannerIds[i] == bannerId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The body as text, or null when it is larger than the limit. Reads at most one byte more than the limit.</summary>
    private async Task<string?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(MaxBodyBytes + 1);
        try
        {
            int total = 0;
            while (total <= MaxBodyBytes)
            {
                int read = await Request.Body.ReadAsync(buffer.AsMemory(total, MaxBodyBytes + 1 - total), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            return total > MaxBodyBytes ? null : Encoding.UTF8.GetString(buffer, 0, total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
