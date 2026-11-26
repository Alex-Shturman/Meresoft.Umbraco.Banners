using Meresoft.Umbraco.Banners.Persistence;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Persistence;

[TestFixture]
public class AuditNodeIdTests
{
    [TestCase(1)]
    [TestCase(1050)]
    [TestCase(1_000_000)]
    [TestCase(int.MaxValue / 2)]
    public void TheMappedId_IsBelowEveryNodeIdUmbracoUses(int entityId) =>
        Assert.That(AuditNodeId.FromEntityId(entityId), Is.LessThan(-1_000_000));

    [Test]
    public void DifferentEntityIds_GiveDifferentNodeIds() =>
        Assert.That(AuditNodeId.FromEntityId(7), Is.Not.EqualTo(AuditNodeId.FromEntityId(8)));

    [Test]
    public void AnIdThatCannotBeMapped_IsRefused() =>
        Assert.That(() => AuditNodeId.FromEntityId(0), Throws.InstanceOf<ArgumentOutOfRangeException>());
}
