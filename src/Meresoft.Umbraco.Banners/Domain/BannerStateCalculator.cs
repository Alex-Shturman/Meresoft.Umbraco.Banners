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

        bool hasUnpublishedChanges = banner.Draft is not null
            && banner.PublishDate is { } published
            && banner.Draft.SavedDate > published;
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

        if (version.StartUtc is { } start && now < start)
        {
            flags |= BannerFlags.Scheduled;
        }

        if (version.EndUtc is { } end && now >= end)
        {
            flags |= BannerFlags.Expired;
        }

        if ((version.ImpressionLimit is { } impressionLimit && info.TotalImpressions >= impressionLimit)
            || (version.ClickLimit is { } clickLimit && info.TotalClicks >= clickLimit))
        {
            flags |= BannerFlags.LimitReached;
        }

        return flags;
    }
}
