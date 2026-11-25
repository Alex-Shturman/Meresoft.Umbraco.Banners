using System.Globalization;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>One impression report line: the view token and the banner that became visible.</summary>
public readonly record struct SeenLine(string Token, int BannerId);

/// <summary>Reads the body of a seen request: lines of <c>{token} {bannerId}</c> (contracts/public-endpoints.md).</summary>
public static class SeenBodyParser
{
    /// <summary>Lines after this many are not read.</summary>
    public const int MaxLines = 50;

    /// <summary>The valid lines among the first <see cref="MaxLines"/> lines; malformed lines are skipped.</summary>
    public static IReadOnlyList<SeenLine> Parse(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return [];
        }

        var lines = new List<SeenLine>();
        int lineCount = 0;
        foreach (Range range in body.AsSpan().Split('\n'))
        {
            if (++lineCount > MaxLines)
            {
                break;
            }

            ReadOnlySpan<char> line = body.AsSpan(range).TrimEnd('\r');
            int space = line.IndexOf(' ');
            if (space <= 0)
            {
                continue;
            }

            // Digits only: no sign, no spaces, no thousands separators.
            if (int.TryParse(line[(space + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int bannerId))
            {
                lines.Add(new SeenLine(line[..space].ToString(), bannerId));
            }
        }

        return lines;
    }
}
