using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;

namespace Meresoft.Umbraco.Banners.Services;

/// <summary>
/// Creates, saves, publishes and deletes banners. Every call runs in its own Umbraco scope. The
/// HTML permission is checked here for the acting user (<c>userKey</c>): creating an Html banner or
/// changing html needs a user who has the permission, so site code such as an importer passes the key of
/// such a user (for example an administrator). A failed attempt for invalid data carries a
/// <see cref="BannerValidationException"/> in its <c>Exception</c>.
/// </summary>
public interface IBannerService
{
    /// <summary>A banner with both versions, its state and its flags; null when it does not exist.</summary>
    Task<Banner?> GetAsync(Guid key);

    /// <summary>A page of the banner list, newest change first.</summary>
    Task<PagedModel<BannerListItem>> GetPagedAsync(BannerQuery query);

    /// <summary>Creates a banner in the Draft state.</summary>
    Task<Attempt<Banner, BannerOperationStatus>> CreateAsync(BannerCreateModel model, Guid userKey);

    /// <summary>Saves the draft only; the live version and the site do not change.</summary>
    Task<Attempt<Banner, BannerOperationStatus>> SaveDraftAsync(Guid key, BannerDraftModel model, Guid userKey);

    /// <summary>Copies the draft over the live version and shows the banner.</summary>
    /// <param name="key">Banner key.</param>
    /// <param name="userKey">Back office user key.</param>
    /// <param name="expectedUpdateDate">The update date the caller loaded; a different stored value gives ChangedByAnotherUser. Null skips the check.</param>
    Task<Attempt<Banner, BannerOperationStatus>> PublishAsync(Guid key, Guid userKey, DateTime? expectedUpdateDate = null);

    /// <summary>Stops showing the banner; the live version stays.</summary>
    Task<Attempt<Banner, BannerOperationStatus>> UnpublishAsync(Guid key, Guid userKey);

    /// <summary>Creates a new draft banner from the draft of an existing one, without statistics.</summary>
    Task<Attempt<Banner, BannerOperationStatus>> CopyAsync(Guid key, string? newName, Guid userKey);

    /// <summary>Deletes the banner; its statistics stay.</summary>
    Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey);
}
