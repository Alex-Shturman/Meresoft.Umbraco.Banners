namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>Derives the publish state and the runtime flags of a banner (data-model.md).</summary>
public sealed class BannerStateCalculator
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the calculator with the clock to use.</summary>
    public BannerStateCalculator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>The publish state of a banner.</summary>
    public BannerState GetState(Banner banner)
    {
        ArgumentNullException.ThrowIfNull(banner);

        if (banner.Live is null)
        {
            return BannerState.Draft;
        }

        if (!banner.IsPublished)
        {
            return BannerState.Unpublished;
        }

        // Publish copies the draft, saved date included, so the two differ exactly when the draft was saved
        // after the last publish. The publish date is not used: it comes from the clock of whichever server
        // published, and clocks of servers differ.
        bool hasUnpublishedChanges = banner.Draft is not null && banner.Draft.SavedDate != banner.Live.SavedDate;
        return hasUnpublishedChanges ? BannerState.PublishedWithChanges : BannerState.Published;
    }

    /// <summary>The runtime flags of a banner, judged on its live version, or on its draft when never published.</summary>
    public BannerFlags GetFlags(Banner banner, BannerRuntimeInfo info)
    {
        ArgumentNullException.ThrowIfNull(banner);

        BannerVersion? version = banner.Live ?? banner.Draft;
        if (version is null)
        {
            return BannerFlags.NotPlaced;
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        BannerFlags flags = BannerFlags.None;

        if (version.ZoneIds.Count == 0)
        {
            flags |= BannerFlags.NotPlaced;
        }

        if (banner.Type == BannerType.Image && !info.MediaExists)
        {
            flags |= BannerFlags.ImageMissing;
        }

        if (!Schedule.HasStarted(version.StartUtc, now))
        {
            flags |= BannerFlags.Scheduled;
        }

        if (Schedule.HasEnded(version.EndUtc, now))
        {
            flags |= BannerFlags.Expired;
        }

        if (BannerLimits.IsReached(version.ImpressionLimit, version.ClickLimit, info.TotalImpressions, info.TotalClicks))
        {
            flags |= BannerFlags.LimitReached;
        }

        return flags;
    }
}
