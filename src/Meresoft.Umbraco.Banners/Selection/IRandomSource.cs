namespace Meresoft.Umbraco.Banners.Selection;

/// <summary>The source of randomness for banner selection, replaceable in tests with a seeded one.</summary>
internal interface IRandomSource
{
    /// <summary>A number from 0 up to, not including, <paramref name="maxExclusive"/>.</summary>
    int Next(int maxExclusive);
}

/// <summary>The default source, over the shared thread safe generator.</summary>
internal sealed class SharedRandomSource : IRandomSource
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
