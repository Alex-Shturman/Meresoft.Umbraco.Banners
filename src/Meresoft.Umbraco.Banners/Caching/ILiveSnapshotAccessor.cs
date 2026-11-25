namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Gives access to the current <see cref="LiveSnapshot"/> and rebuilds it.</summary>
public interface ILiveSnapshotAccessor
{
    /// <summary>The current snapshot; an empty one until the first build. Never queries the database.</summary>
    LiveSnapshot Current { get; }

    /// <summary>Reads zones and published banners from the database and swaps in a new snapshot.</summary>
    Task RebuildAsync();
}
