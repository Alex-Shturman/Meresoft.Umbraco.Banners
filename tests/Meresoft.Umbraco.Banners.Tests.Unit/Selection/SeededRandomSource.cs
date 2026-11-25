using Meresoft.Umbraco.Banners.Selection;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Selection;

/// <summary>A repeatable random source for statistical tests.</summary>
internal sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed) => _random = new Random(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}
