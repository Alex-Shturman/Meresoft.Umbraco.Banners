using Meresoft.Umbraco.Banners.Persistence;
using Meresoft.Umbraco.Banners.Services.Audit;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Services;

[TestFixture]
public class BannerAuditWriterTests
{
    private static readonly Guid User = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private IAuditService _audit = null!;
    private BannerAuditWriter _writer = null!;

    [SetUp]
    public void SetUp()
    {
        _audit = Substitute.For<IAuditService>();
        _audit.AddAsync(default, default, default, default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult(Attempt.Succeed(AuditLogOperationStatus.Success)));
        _writer = new BannerAuditWriter(_audit);
    }

    [TestCase(BannerAuditAction.New, AuditType.New, "new")]
    [TestCase(BannerAuditAction.Save, AuditType.Save, "save")]
    [TestCase(BannerAuditAction.Publish, AuditType.Publish, "publish")]
    [TestCase(BannerAuditAction.Unpublish, AuditType.Unpublish, "unpublish")]
    [TestCase(BannerAuditAction.Copy, AuditType.Copy, "copy")]
    [TestCase(BannerAuditAction.Delete, AuditType.Delete, "delete")]
    public async Task Banner_WritesTheAuditTypeAndEntityType(BannerAuditAction action, AuditType expectedType, string word)
    {
        await _writer.WriteBannerAsync(action, User, 12);

        await _audit.Received(1).AddAsync(
            expectedType,
            User,
            AuditNodeId.FromEntityId(12),
            "meresoft-banner",
            Arg.Is<string>(c => c.Contains(word) && c.Contains("12")),
            Arg.Any<string?>());
    }

    [Test]
    public async Task Zone_UsesTheZoneEntityType()
    {
        await _writer.WriteZoneAsync(BannerAuditAction.Save, User, 3);

        await _audit.Received(1).AddAsync(
            AuditType.Save,
            User,
            AuditNodeId.FromEntityId(3),
            "meresoft-banner-zone",
            Arg.Is<string>(c => c.Contains("save") && c.Contains("3")),
            Arg.Any<string?>());
    }

    [Test]
    public async Task Comment_HoldsOnlyTheActionNameAndAnId()
    {
        string? comment = null;
        _audit.AddAsync(Arg.Any<AuditType>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Do<string>(c => comment = c), Arg.Any<string?>())
            .Returns(Task.FromResult(Attempt.Succeed(AuditLogOperationStatus.Success)));

        await _writer.WriteBannerAsync(BannerAuditAction.Publish, User, 12);

        Assert.That(comment, Is.EqualTo("publish banner 12"));
    }
}
