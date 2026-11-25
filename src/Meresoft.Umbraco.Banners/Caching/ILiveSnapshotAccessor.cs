namespace Meresoft.Umbraco.Banners.Caching;

/// <summary>Gives access to the current <see cref="LiveSnapshot"/> and rebuilds it.</summary>
public interface ILiveSnapshotAccessor
{
    /// <summary>The current snapshot; an empty one until the first build. Never queries the database.</summary>
    LiveSnapshot Current { get; }

    /// <summary>False until the first build has succeeded; the empty snapshot before that is not a real answer.</summary>
    bool IsBuilt { get; }

    /// <summary>
    /// Called when the start up build is over, whether it worked or was given up. From then on a read before the first
    /// successful build is worth a warning; before that it is only the short normal window while the site starts.
    /// </summary>
    void MarkStartupBuildFinished();

    /// <summary>Reads zones and published banners from the database and swaps in a new snapshot.</summary>
    /// <param name="mediaKeysTreatedAsMissing">Media items known to be gone; they count as missing without asking the media cache, which may not have caught up yet.</param>
    Task RebuildAsync(IReadOnlySet<Guid>? mediaKeysTreatedAsMissing = null);

    /// <summary>
    /// Reads the totals of the banners in the snapshot that have a limit and swaps in a snapshot without those that
    /// reached it. Cheap: no zones, banners or media are read. Called after each flush (US5).
    /// A reload only ever removes banners. Totals that go down (a Replace import of statistics, T119) do not bring a
    /// banner back; that needs a full <see cref="RebuildAsync"/>.
    /// </summary>
    Task ReloadLimitsAsync(CancellationToken cancellationToken = default);
}
