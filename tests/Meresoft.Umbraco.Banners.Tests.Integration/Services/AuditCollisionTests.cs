using Meresoft.Umbraco.Banners.Services.Audit;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// Banner and zone ids share the integer space of Umbraco's node ids. The audit rows of the package must never be
/// read as part of a media item's or document's history, which Umbraco reads by node id only.
/// </summary>
[TestFixture]
public class AuditCollisionTests : BannersServiceTestBase
{
    [Test]
    public async Task ABannerWithTheSameIdAsAMediaItem_DoesNotAppearInTheMediaHistory()
    {
        IMedia media = TestMedia.CreateImage(GetRequiredService<IMediaService>());
        var writer = new BannerAuditWriter(GetRequiredService<IAuditService>());

        await writer.WriteBannerAsync(BannerAuditAction.Save, User, media.Id);
        await writer.WriteZoneAsync(BannerAuditAction.Save, User, media.Id);

        IAuditItem[] mediaHistory = (await GetRequiredService<IAuditService>().GetItemsByEntityAsync(media.Id, 0, 100)).Items.ToArray();
        Assert.That(StoredAudit.For(ScopeProvider, media.Id, BannersConstants.BannerEntityType), Has.Length.EqualTo(1), "the package rows exist, under their own ids");
        Assert.That(
            mediaHistory.Select(i => i.EntityType),
            Has.None.EqualTo(BannersConstants.BannerEntityType).And.None.EqualTo(BannersConstants.ZoneEntityType),
            "package rows must not show in the history of a node with the same id");
    }
}
