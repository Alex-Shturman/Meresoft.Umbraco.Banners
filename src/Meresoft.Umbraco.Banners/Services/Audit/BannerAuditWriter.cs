using Meresoft.Umbraco.Banners.Persistence;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Services.Audit;

/// <summary>
/// Writes banner and zone changes to the Umbraco audit log (FR-018). The comment holds the action
/// name and the id only, never content, so no personal data enters the log.
/// </summary>
internal sealed class BannerAuditWriter
{
    private readonly IAuditService _auditService;

    public BannerAuditWriter(IAuditService auditService)
    {
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    /// <summary>Records an action on a banner.</summary>
    public Task WriteBannerAsync(BannerAuditAction action, Guid userKey, int bannerId) =>
        WriteAsync(action, userKey, bannerId, BannersConstants.BannerEntityType, "banner");

    /// <summary>Records an action on a zone.</summary>
    public Task WriteZoneAsync(BannerAuditAction action, Guid userKey, int zoneId) =>
        WriteAsync(action, userKey, zoneId, BannersConstants.ZoneEntityType, "zone");

    private Task WriteAsync(BannerAuditAction action, Guid userKey, int id, string entityType, string noun) =>
        _auditService.AddAsync(ToAuditType(action), userKey, AuditNodeId.FromEntityId(id), entityType, $"{action.ToString().ToLowerInvariant()} {noun} {id}", null);

    private static AuditType ToAuditType(BannerAuditAction action) => action switch
    {
        BannerAuditAction.New => AuditType.New,
        BannerAuditAction.Save => AuditType.Save,
        BannerAuditAction.Publish => AuditType.Publish,
        BannerAuditAction.Unpublish => AuditType.Unpublish,
        BannerAuditAction.Copy => AuditType.Copy,
        BannerAuditAction.Delete => AuditType.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown audit action."),
    };
}
